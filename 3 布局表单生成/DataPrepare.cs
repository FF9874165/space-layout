using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using Eto.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    //生成各层建筑列表
    public class DataPrepare : GH_Component
    {
        public DataPrepare()
          : base("建筑布局数据准备", "数据准备",
              "各类型场馆进行空间布局的数据准备",
               "建筑空间布局", "布局生成")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("建筑场馆", "场馆", "本项目中包括的建筑场馆类型", GH_ParamAccess.list);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("运行状态", "运行状态", "本轮布局是否成功", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region 变量设置
            //外部变量
            List<Building> buildings = new List<Building>();//获取本项目涉及的所有场馆
            //待布置场馆单体集合
            List<ITrans> buildingGroups = new List<ITrans>();
            List<ITrans> lobbies = new List<ITrans>();

            if (!DA.GetDataList(0, buildings)) return;

            //排除本工程中不设置的场馆
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] == null)
                {
                    buildings.Remove(buildings[i]);
                    i -= 1;
                }
            }
            //获取待布置场馆单体集合
            for (int i = 0; i < buildings.Count; i++)
            {
                switch (buildings[i].buildingType)
                {
                    case BuildingType.篮球比赛馆:
                        buildingGroups.Add(buildings[i] as BasketballMatchBuilding);
                        break;
                    case BuildingType.篮球训练馆:
                        for (int j = 0; j < (buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups.Count; j++)
                        {
                            buildingGroups.Add((buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups[j]);
                        }
                        break;
                    case BuildingType.游泳馆:
                        for (int j = 0; j < (buildings[i] as AquaticBuilding).aquaticBuildingGroup.Count; j++)
                        {
                            buildingGroups.Add((buildings[i] as AquaticBuilding).aquaticBuildingGroup[j]);
                        }
                        break;
                    case BuildingType.网球馆:
                        for (int j = 0; j < (buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups.Count; j++)
                        {
                            buildingGroups.Add((buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups[j]);
                        }
                        break;
                    case BuildingType.羽毛球馆:
                        for (int j = 0; j < (buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups.Count; j++)
                        {
                            buildingGroups.Add((buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups[j]);
                        }
                        break;
                    case BuildingType.乒乓球馆:
                        for (int j = 0; j < (buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups.Count; j++)
                        {
                            buildingGroups.Add((buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups[j]);
                        }
                        break;
                    case BuildingType.冰球馆:
                        for (int j = 0; j < (buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups.Count; j++)
                        {
                            buildingGroups.Add((buildings[i] as GeneralCourtBuilding).generalCourtBuildingGroups[j]);
                        }
                        break;
                    case BuildingType.健身馆:
                        for (int j = 0; j < (buildings[i] as Gymnasium).gymnasiumGroup.Count; j++)
                        {
                            buildingGroups.Add((buildings[i] as Gymnasium).gymnasiumGroup[j]);
                        }
                        break;
                    case BuildingType.办公:
                        buildingGroups.Add(buildings[i] as Office);
                        break;
                    case BuildingType.观演厅:
                        buildingGroups.Add(buildings[i] as Theater);

                        break;
                    case BuildingType.其他:
                        buildingGroups.Add(buildings[i] as OtherFunction);
                        break;
                    case BuildingType.大厅:
                        for (int j = 0; j < (buildings[i] as Lobby).lobbyUnits.Count; j++)
                        {
                            lobbies.Add((buildings[i] as Lobby).lobbyUnits[j]);
                        }
                        break;
                }
            }

            //复制场馆单体集合，获得未布置场馆列表
            List<ITrans> buildingToBeArranged = new List<ITrans>(buildingGroups);
            List<ITrans> lobbyToBeArranged = new List<ITrans>(lobbies);
            List<List<ITrans>> buildingPerFloor = new List<List<ITrans>>();//各层建筑单体列表
            int prepareCount = 15;
            for (int i = 0; i < prepareCount; i++)//避免索引报错
            {
                buildingPerFloor.Add(null);
            }
            List<Curve[]> floorBoundary = new List<Curve[]>(prepareCount);//各层布局边界
            for (int i = 0; i < prepareCount; i++)//避免索引报错
            {
                floorBoundary.Add(null);
            }
            List<double> areaPerFloorActual = new List<double>();//各层实际面积
            for (int i = 0; i < prepareCount; i++)//避免索引报错
            {
                areaPerFloorActual.Add(0);
            }
            List<double> areaPerFloorMax = new List<double>();//各层最大计算面积
            for (int i = 0; i < prepareCount; i++)//避免索引报错
            {
                areaPerFloorMax.Add(0);
            }
            List<double> areaIndex = new List<double>();//各层面积布置最大值边界与计算最大值的比值
            for (int i = 0; i < prepareCount; i++)//避免索引报错
            {
                areaIndex.Add(1);
            }

            int isSuccess = 0;//本轮试算是否成功,0:正在尝试；1：成功；2：失败
            int failTimes = 0;//分层布置时，单层连续失败次数
            bool groundFloorOk = false;//用于判定本层布局是否完成
            int currentFloor = 0;//用于增加待布置楼层数
            #endregion

            #region 场馆布局过程
            while ((buildingToBeArranged.Count > 0) && (failTimes < 2))
            {
                isSuccess = 0;
                //添加首层
                if (buildingPerFloor[0] == null)
                {
                    List<ITrans> groundFloor = new List<ITrans>();
                    buildingPerFloor.Add(groundFloor);
                    areaIndex[0] = 0.76;
                    areaPerFloorMax[0] = SiteInfo.largestGroundFloorArea;//首层不大于计算值
                    floorBoundary[0] = new Curve[] { SiteInfo.siteBoundary.Value };//首层边界
                }
                //首层场馆布置
                if ((buildingPerFloor[0] == null) && (groundFloorOk == false))
                {
                    buildingPerFloor[0] = new List<ITrans> { lobbyToBeArranged[0] };//增加首层大厅
                    areaPerFloorActual[0] += (lobbyToBeArranged[0] as LobbyUnit).baseArea;//首层增添该大厅面积
                    AddCeilingBoundary(lobbyToBeArranged[0], floorBoundary, currentFloor, prepareCount);//将大厅顶面线加入上层布局边界
                    lobbyToBeArranged.RemoveAt(0);//从大厅待布置列表中移除该大厅
                    int tryTime = 0;

                    while ((areaPerFloorActual[0] < areaPerFloorMax[0] * areaIndex[0]) && (isSuccess == 0))
                    {
                        //每轮布置随机一个新种子的Random
                        Random random = new Random(Tool.random.Next());
                        int index = random.Next(buildingToBeArranged.Count);
                        ITrans toBeLayout = buildingToBeArranged[index];
                        if (((areaPerFloorActual[0] + GetInfo(toBeLayout, 0)) < areaPerFloorMax[0]) && (tryTime <= 5))//面积、尝试次数均不超
                        {
                            buildingPerFloor[0].Add(toBeLayout);//该单体加入首层建筑表单
                            areaPerFloorActual[0] += GetInfo(toBeLayout, 0);//首层增添该面积
                            buildingToBeArranged.RemoveAt(index);//从运动场馆待布置列表中移除该场馆
                            AddCeilingBoundary(toBeLayout, floorBoundary, currentFloor, prepareCount);//将顶面线加入上层布局边界

                        }
                        else
                        {
                            if (tryTime > 5)//尝试次数超了，放弃本轮
                            {
                                isSuccess = 2;
                                failTimes += 1;
                            }
                            tryTime += 1;
                        }
                    }
                    groundFloorOk = true;
                    currentFloor += 1;
                }
                //添加楼层
                else
                {
                    if (floorBoundary[currentFloor] != null)//该标高存在可布置屋面
                    {
                        #region 数据准备
                        List<ITrans> newFloor = new List<ITrans>();
                        buildingPerFloor[currentFloor] = newFloor;
                        if (currentFloor == 2)//针对项目的写法，12米标高，该层继续加入场馆的准入门槛（原先0.75）
                        {
                            areaIndex[currentFloor] = 0.8;//该层继续加入场馆的准入门槛（原先0.75）
                        }
                        else//非12米标高
                        {
                            areaIndex[currentFloor] = 0.6;//该层继续加入场馆的准入门槛
                        }
                        areaPerFloorMax[currentFloor] = GetBoundaryArea(floorBoundary[currentFloor]);//该层不大于计算值
                        int tryTime = 0;
                        #endregion

                        #region 添加大厅
                        if ((currentFloor % 2 == 0) && lobbyToBeArranged.Count > 0)
                        {
                            buildingPerFloor[currentFloor].Add(lobbyToBeArranged[0]);//增加大厅
                            areaPerFloorActual[currentFloor] += (lobbyToBeArranged[0] as LobbyUnit).baseArea;//首层增添该大厅面积
                            AddCeilingBoundary(lobbyToBeArranged[0], floorBoundary, currentFloor, prepareCount);//将大厅顶面线加入上层布局边界
                            lobbyToBeArranged.RemoveAt(0);//从大厅待布置列表中移除该大厅
                        }
                        #endregion

                        #region 场馆布置
                        while ((areaPerFloorActual[currentFloor] < areaPerFloorMax[currentFloor] * areaIndex[currentFloor]) && ((buildingToBeArranged.Count > 0) ||(lobbyToBeArranged.Count>0))&& (isSuccess == 0))
                        {
                            //每轮布置随机一个新种子的Random
                            Random random = new Random(Tool.random.Next());
                            int index = random.Next(buildingToBeArranged.Count);
                            ITrans toBeLayout = buildingToBeArranged[index];
                            if (currentFloor % 2 == 0)//针对项目的写法，偶数层标高，面积变动大
                            {
                                if (((areaPerFloorActual[currentFloor] + GetInfo(toBeLayout, 0)) <= areaPerFloorMax[currentFloor] * 0.9) && (tryTime <= 8))//面积、尝试次数均不超
                                {
                                    buildingPerFloor[currentFloor].Add(toBeLayout);//该单体加入该层建筑表单
                                    areaPerFloorActual[currentFloor] += GetInfo(toBeLayout, 0);//该层增添面积
                                    buildingToBeArranged.RemoveAt(index);//从运动场馆待布置列表中移除该场馆
                                    AddCeilingBoundary(toBeLayout, floorBoundary, currentFloor, prepareCount);//将顶面线加入上层布局边界
                                }
                                else
                                {
                                    if (tryTime > 8)//尝试次数超了，放弃本轮
                                    {
                                        if (buildingToBeArranged.Count==1)//【Fixed】就剩1个场馆怎么也放不下时，加1楼层
                                        {
                                            currentFloor++;
                                        }
                                        failTimes += 1;
                                        //连续2次布置失败，结束本轮布置
                                        if (failTimes > 2) 
                                        {
                                            isSuccess = 2;
                                            Manager.restart=true;
                                        }
                                    }
                                    tryTime += 1;
                                }
                            }
                            else//奇数层标高
                            {
                                if (((areaPerFloorActual[currentFloor] + GetInfo(toBeLayout, 0)) <= areaPerFloorMax[currentFloor] * 0.8) && (tryTime <= 8))//面积、尝试次数均不超
                                {
                                    buildingPerFloor[currentFloor].Add(toBeLayout);//该单体加入该层建筑表单
                                    areaPerFloorActual[currentFloor] += GetInfo(toBeLayout, 0);//该层增添面积
                                    buildingToBeArranged.RemoveAt(index);//从运动场馆待布置列表中移除该场馆
                                    AddCeilingBoundary(toBeLayout, floorBoundary, currentFloor, prepareCount);//将顶面线加入上层布局边界
                                }
                                else
                                {
                                    if (tryTime > 8)//尝试次数超了，放弃本轮
                                    {
                                        if (buildingToBeArranged.Count == 1)//【Fixed】就剩1个场馆怎么也放不下时，加1楼层
                                        {
                                            currentFloor++;
                                        }
                                        failTimes += 1;
                                        //连续2次布置失败，结束本轮布置
                                        if (failTimes > 2)
                                        {
                                            isSuccess = 2;
                                            Manager.restart = true;
                                        }
                                    }
                                    tryTime += 1;
                                }
                            }
                        }
                        #endregion
                    }
                    currentFloor += 1;
                }
            }

            //超高检测
            for (int i = 1; i < buildingPerFloor.Count; i++)
            {
                if (buildingPerFloor[i] != null)
                {
                    for (int j = 0; j < buildingPerFloor[i].Count; j++)
                    {
                        if (buildingPerFloor[i][j] != null)
                        {
                            BuildingType tempType = Tool.GetBuildingType(buildingPerFloor[i][j]);
                            double height = 0;
                            if ((tempType == BuildingType.冰球馆) || (tempType == BuildingType.大厅) || (tempType == BuildingType.网球馆) || (tempType == BuildingType.游泳馆) || (tempType == BuildingType.羽毛球馆) || (tempType == BuildingType.观演厅))
                            {
                                height = 12;
                            }
                            else if ((tempType == BuildingType.篮球训练馆) || (tempType == BuildingType.篮球比赛馆))
                            {
                                height = 24;
                            }
                            else
                            {
                                height = 6;
                            }
                            if (6 * i + height > SiteInfo.siteHeight)
                            {
                                isSuccess = 2;
                                Manager.restart=true;//本次空间布局失败
                                break;
                            }
                        }
                    }
                }
            }

            //数据更新
            if (isSuccess == 0) isSuccess = 1;//更新状态
            StaticObject.buildingToBeArranged = buildingToBeArranged;
            StaticObject.lobbyToBeArranged = lobbyToBeArranged;
            StaticObject.buildingPerFloor = buildingPerFloor;
            StaticObject.floorBoundary = floorBoundary;
            StaticObject.areaPerFloorActual = areaPerFloorActual;
            #endregion

            Manager.calculationTimes = 0;

            //输出参数
            DA.SetData(0, isSuccess);
        }
        //获取顶、底面积。0:baseBoundary;1:ceilingBoundary;2:height
        public double GetInfo(ITrans toBeLayout, int dataType)
        {
            if (dataType == 0)//求baseBoundary
            {
                if (toBeLayout is BasketballMatchBuilding)
                {
                    return (toBeLayout as BasketballMatchBuilding).baseArea;
                }
                else if (toBeLayout is BuildingGroup)
                {
                    return (toBeLayout as BuildingGroup).baseArea;
                }
                else if (toBeLayout is Office)
                {
                    return (toBeLayout as Office).baseArea;
                }
                else if (toBeLayout is Theater)
                {
                    return (toBeLayout as Theater).baseArea;
                }
                else
                {
                    return (toBeLayout as OtherFunction).baseArea;
                }
            }
            else if (dataType == 1)//求ceilingBoundary
            {
                if (toBeLayout is BasketballMatchBuilding)
                {
                    return (toBeLayout as BasketballMatchBuilding).ceilingArea;
                }
                else if (toBeLayout is BuildingGroup)
                {
                    return (toBeLayout as BuildingGroup).ceilingArea;
                }
                else if (toBeLayout is Office)
                {
                    return (toBeLayout as Office).ceilingArea;
                }
                else if (toBeLayout is Theater)
                {
                    return (toBeLayout as Theater).ceilingArea;
                }
                else
                {
                    return (toBeLayout as OtherFunction).ceilingArea;
                }
            }
            else
            {
                if (toBeLayout is BasketballMatchBuilding)
                {
                    return (toBeLayout as BasketballMatchBuilding).height;
                }
                else if (toBeLayout is BuildingGroup)
                {
                    return (toBeLayout as BuildingGroup).groupBoundary.Value.Z.Max;
                }
                else if (toBeLayout is Office)
                {
                    return (toBeLayout as Office).height;
                }
                else if (toBeLayout is Theater)
                {
                    return (toBeLayout as Theater).height;
                }
                else if (toBeLayout is LobbyUnit)
                {
                    return (toBeLayout as LobbyUnit).height;
                }
                else
                {
                    return (toBeLayout as OtherFunction).height;
                }
            }
        }
        //增加边界Curve到指定层
        public void AddCeilingBoundary(ITrans toBeLayout, List<Curve[]> floorBoundary, int currentFloor, int prepareCount)
        {
            if (floorBoundary[(int)GetInfo(toBeLayout, 2) / 6 + currentFloor] == null)
            {
                floorBoundary[(int)GetInfo(toBeLayout, 2) / 6 + currentFloor] = new Curve[prepareCount];
                floorBoundary[(int)GetInfo(toBeLayout, 2) / 6 + currentFloor][0] = GetBuildingBoundary(toBeLayout, 1);
            }
            else
            {
                for (int i = 0; i < floorBoundary[(int)GetInfo(toBeLayout, 2) / 6 + currentFloor].Length; i++)
                {
                    if (floorBoundary[(int)GetInfo(toBeLayout, 2) / 6 + currentFloor][i] == null)
                    {
                        floorBoundary[(int)GetInfo(toBeLayout, 2) / 6 + currentFloor][i] = GetBuildingBoundary(toBeLayout, 1);
                        return;
                    }
                }
            }
        }
        //获取顶、底面积。0:baseBoundary;1:ceilingBoundary
        public Curve GetBuildingBoundary(ITrans toBeLayout, int dataType)
        {
            if (dataType == 0)//求baseBoundary
            {
                if (toBeLayout is BasketballMatchBuilding)
                {
                    return (toBeLayout as BasketballMatchBuilding).baseBoundary;
                }
                else if (toBeLayout is BuildingGroup)
                {
                    return (toBeLayout as BuildingGroup).baseBoundary;
                }
                else if (toBeLayout is Office)
                {
                    return (toBeLayout as Office).baseBoundary;
                }
                else if (toBeLayout is Theater)
                {
                    return (toBeLayout as Theater).baseBoundary;
                }
                else if (toBeLayout is LobbyUnit)
                {
                    return (toBeLayout as LobbyUnit).baseBoundary;
                }
                else
                {
                    return (toBeLayout as OtherFunction).baseBoundary;
                }
            }
            else//求ceilingBoundary
            {
                if (toBeLayout is BasketballMatchBuilding)
                {
                    return (toBeLayout as BasketballMatchBuilding).ceilingBoundary;
                }
                else if (toBeLayout is BuildingGroup)
                {
                    return (toBeLayout as BuildingGroup).ceilingBoundary;
                }
                else if (toBeLayout is Office)
                {
                    return (toBeLayout as Office).ceilingBoundary;
                }
                else if (toBeLayout is Theater)
                {
                    return (toBeLayout as Theater).ceilingBoundary;
                }
                else if (toBeLayout is LobbyUnit)
                {
                    return (toBeLayout as LobbyUnit).ceilingBoundary;
                }
                else
                {
                    return (toBeLayout as OtherFunction).ceilingBoundary;
                }
            }
        }
        //获取当前层边界最大面积
        public double GetBoundaryArea(Curve[] boundary)
        {
            double sum = 0;
            int test = boundary.Length;
            for (int i = 0; i < boundary.Length; i++)
            {
                if (boundary[i] != null)
                {
                    AreaMassProperties compute = AreaMassProperties.Compute(boundary[i]);
                    sum += compute.Area;
                }
            }

            return sum;
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources._8_数据准备;

        public override Guid ComponentGuid
        {
            get { return new Guid("A5A44AF1-B551-4F8F-98AF-2F8CB9762F39"); }
        }
    }
}