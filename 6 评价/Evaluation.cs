using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Microsoft.Win32;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.SS.Util;
using Eto.Forms;
using Newtonsoft.Json;


namespace Space_Layout
{
    //方案评估并写入EXCEL和JSON
    public class Evaluation : GH_Component
    {
        public Evaluation()
          : base("方案评价", "评价",
              "针对本轮完成的空间布局进行指标计算及评价，作为方案筛选的数据支持",
              "建筑空间布局", "方案评价")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("上一步骤是否成功完成", "运行状态", "二层及以上的布局移动是否形成有效方案，2=失败，1=成功", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("", "指标计算", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region 数据准备
            int isSuccess = 9;//生成状态，用于进程管理
            if (!DA.GetData(0, ref isSuccess)) return;
            #endregion

            List<string> strings = new List<string>();

            if (isSuccess == 1)//若上一步形成了有效的方案
            {
                ////注册函数到重新生成的事件+
                //Manager.restartEvent += Restart;

                if ((Manager.ifEvaluateOK == false)&&(Manager.ifAvoidCalculateTwice==false))
                {
                    #region 单项指标方案评价
                    StaticObject.areaRequired = new List<List<double>>();
                    StaticObject.areaActual = new List<List<double>>();
                    StaticObject.areaDelta = new List<List<double>>();
                    for (int i = 0; i < StaticObject.buildingPerFloor.Count; i++)//搭建面积存储位置
                    {
                        StaticObject.areaRequired.Add(new List<double>());
                        StaticObject.areaActual.Add(new List<double>());
                        StaticObject.areaDelta.Add(new List<double>());
                        if (StaticObject.buildingPerFloor[i] != null)
                        {
                            for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                            {
                                if (StaticObject.buildingPerFloor[i][j] != null)
                                {
                                    //参数添加占位
                                    GetBuildingIndex(i, j);
                                    //获取基础面积，未考虑场馆与大厅重叠+悬挑超长
                                    GetBasicArea(i, j);
                                }
                            }
                        }
                    }

                    // 建筑面积，减去大厅与场馆的重叠后
                    LobbyMinusIntersection();

                    //计算面积浮动率
                    CalculateAreaFluctuationRate();

                    //计算悬挑超出极限位置的面积除以任务书面积
                    CalculateOverLimitationArea();

                    // 容积率
                    GetBuildingDensity();

                    // 首层轮廓线长度除以面积
                    GetCompactness();

                    // 统计与大厅相邻场馆的占比
                    CalculateConnectionRate();

                    //判断游泳馆与滑冰馆是否相邻(节能策略)
                    IfIceHockeyBuildingNearAquaticBuilding();

                    //计算相邻功能的场馆/纯在拆分现象场馆数量之和（不计共享大厅）
                    GetSameFunctionAjoinRate();

                    //计算体型系数
                    CalculateShapeFactor();
                    #endregion

                    #region 判断是否符合导出条件，针对体型系数存在计算值无效的情况
                    //若体型系数模型有效，则导出数据
                    if (StaticObject.unionResult != null)
                    {
                        //成功次数累加
                        Manager.runningTime += 1;
                        Manager.successTime+=1;
                        Manager.ifEvaluateOK = true;

                        //数据写入excel
                        WriteExcelFile();

                    }
                    //若体型系数模型无效，则终止本轮
                    else
                    {
                        Manager.restart = true;
                        Manager.runningTime++;
                        return;
                    }
                    #endregion

                    #region 写入JSON
                    //待写入的简化建筑列表
                    List< SimplifiedBuilding > simplifiedBuildings= new List< SimplifiedBuilding >();
                    //创建各场馆的简化对象
                    for (int i = 0; i < StaticObject.buildingPerFloor.Count; i++)
                    {
                        if (StaticObject.buildingPerFloor[i]!=null)
                        {
                            for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                            {
                                if (StaticObject.buildingPerFloor[i][j] != null)
                                {
                                    simplifiedBuildings.Add(StaticObject.buildingPerFloor[i][j].CreateSimplifiedBuilding());
                                }
                            }
                        }
                    }
                    //将简化对象写入JSON
                    string outputJSON = JsonConvert.SerializeObject(simplifiedBuildings);
                    //构建文件路径
                    string filePath = "C:\\Users\\Administrator\\Desktop\\SpaceLayout";
                    //文件夹目录是否存在，若不存在则新建一个
                    if (!Directory.Exists(filePath))
                    {
                        Directory.CreateDirectory(filePath);
                    }
                    //创建文件名
                    string fileName = Manager.successTime.ToString()+ ".json";
                    string fileFullPath = Path.Combine(filePath, fileName);
                    //写出文件
                    File.WriteAllText(fileFullPath, outputJSON);
                    #endregion

                    Manager.ifEvaluateOK = true;
                }

                //test

                strings.Add("面积浮动率：" + StaticObject.areaFluctuationRate.ToString());
                strings.Add("建筑密度：" + SiteInfo.buildingDensityActual.ToString());
                strings.Add("体型系数：" + StaticObject.shapeFactor.ToString());
                strings.Add("边长除面积：" + StaticObject.lengthDivideArea.ToString());
                strings.Add("与大厅连接率：" + StaticObject.connectionRate.ToString());
                strings.Add("相同功能贴临率：" + StaticObject.sameFunctionAdjoinRate.ToString());
                strings.Add("悬挑面积占比：" + StaticObject.areaOverLimitationRate.ToString());
                strings.Add("冰场与游泳馆贴临：" + StaticObject.ifEnergyConservation.ToString());

                DA.SetDataList(0, strings);
            }
        }

        //Manager监听的方法，当本轮生成结束后调用
        public void Restart()
        {
            OnPingDocument().ScheduleSolution(StaticObject.timeSpan, new GH_Document.GH_ScheduleDelegate(this.ScheduleCallback));
        }
        //重启下一轮计算的方法
        private void ScheduleCallback(GH_Document doc)
        {
            ExpireSolution(false);
        }
        //将每个独立场馆的哪层、哪个编号内置于该对象内
        public void GetBuildingIndex(int currentLevel, int itemIndex)
        {
            int groupNumber;

            #region 若是0标高处
            if (currentLevel == 0)
            {
                switch (StaticObject.buildingTypes[itemIndex])
                {
                    case BuildingType.篮球比赛馆:
                        StaticObject.basketballMatchBuilding.currentLevel = currentLevel;//位于哪层
                        StaticObject.basketballMatchBuilding.itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.篮球训练馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.游泳馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].currentLevel = currentLevel;
                        StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.羽毛球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.网球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.冰球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.乒乓球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.健身馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.gymnasium.gymnasiumGroup[groupNumber].currentLevel = currentLevel;
                        StaticObject.gymnasium.gymnasiumGroup[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.办公:
                        StaticObject.office.currentLevel = currentLevel;//位于哪层
                        StaticObject.office.itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.观演厅:
                        StaticObject.theater.currentLevel = currentLevel;//位于哪层
                        StaticObject.theater.itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.大厅:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as LobbyUnit).groupNumber;
                        StaticObject.lobby.lobbyUnits[groupNumber].currentLevel = currentLevel;//位于哪层
                        StaticObject.lobby.lobbyUnits[groupNumber].itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.其他:
                        StaticObject.otherFunction.currentLevel = currentLevel;//位于哪层
                        StaticObject.otherFunction.itemIndex = itemIndex;//位于该层第几个
                        break;
                    default:
                        break;
                }
            }
            #endregion

            #region 0标高以上的各楼层
            else
            {
                switch (StaticObject.buildingTypesUpFloor[currentLevel][itemIndex])
                {
                    case BuildingType.篮球比赛馆:
                        StaticObject.basketballMatchBuilding.currentLevel = currentLevel;//位于哪层
                        StaticObject.basketballMatchBuilding.itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.篮球训练馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.游泳馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].currentLevel = currentLevel;
                        StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.羽毛球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.网球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.冰球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.乒乓球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].currentLevel = currentLevel;
                        StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.健身馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.gymnasium.gymnasiumGroup[groupNumber].currentLevel = currentLevel;
                        StaticObject.gymnasium.gymnasiumGroup[groupNumber].itemIndex = itemIndex;
                        break;
                    case BuildingType.办公:
                        StaticObject.office.currentLevel = currentLevel;//位于哪层
                        StaticObject.office.itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.观演厅:
                        StaticObject.theater.currentLevel = currentLevel;//位于哪层
                        StaticObject.theater.itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.大厅:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as LobbyUnit).groupNumber;
                        StaticObject.lobby.lobbyUnits[groupNumber].currentLevel = currentLevel;//位于哪层
                        StaticObject.lobby.lobbyUnits[groupNumber].itemIndex = itemIndex;//位于该层第几个
                        break;
                    case BuildingType.其他:
                        StaticObject.otherFunction.currentLevel = currentLevel;//位于哪层
                        StaticObject.otherFunction.itemIndex = itemIndex;//位于该层第几个
                        break;
                    default:
                        break;
                }
            }
            #endregion
        }
        //获取建筑面积情况
        public void GetBasicArea(int currentLevel, int itemIndex)
        {
            int groupNumber;
            #region 若是0标高处
            if (currentLevel == 0)
            {
                switch (StaticObject.buildingTypes[itemIndex])
                {
                    case BuildingType.篮球比赛馆:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.basketballMatchBuilding.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.basketballMatchBuilding.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.篮球训练馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.游泳馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.羽毛球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.网球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.冰球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.乒乓球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.健身馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.gymnasium.gymnasiumGroup[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.gymnasium.gymnasiumGroup[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.办公:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.office.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.office.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.观演厅:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.theater.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.theater.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.大厅:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as LobbyUnit).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.lobby.lobbyUnits[groupNumber].area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.lobby.lobbyUnits[groupNumber].area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.其他:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.otherFunction.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.otherFunction.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    default:
                        break;
                }
            }
            #endregion

            #region 0标高以上的各楼层
            else
            {
                switch (StaticObject.buildingTypesUpFloor[currentLevel][itemIndex])
                {
                    case BuildingType.篮球比赛馆:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.basketballMatchBuilding.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.basketballMatchBuilding.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.篮球训练馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.basketballTrainingBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.游泳馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.aquaticBuilding.aquaticBuildingGroup[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.羽毛球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.badmintonBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.网球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.tennisBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.冰球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.乒乓球馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as GeneralCourtBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.tableTennisBuilding.generalCourtBuildingGroups[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.健身馆:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as SportsBuildingGroup).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.gymnasium.gymnasiumGroup[groupNumber].areaRequired);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.gymnasium.gymnasiumGroup[groupNumber].areaActual);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.办公:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.office.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.office.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.观演厅:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.theater.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.theater.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.大厅:
                        groupNumber = (StaticObject.buildingPerFloor[currentLevel][itemIndex] as LobbyUnit).groupNumber;
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.lobby.lobbyUnits[groupNumber].area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.lobby.lobbyUnits[groupNumber].area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    case BuildingType.其他:
                        StaticObject.areaRequired[currentLevel].Add(StaticObject.otherFunction.area);
                        StaticObject.areaActual[currentLevel].Add(StaticObject.otherFunction.area);
                        StaticObject.areaDelta[currentLevel].Add(StaticObject.areaRequired[currentLevel][itemIndex] - StaticObject.areaActual[currentLevel][itemIndex]);
                        break;
                    default:
                        break;
                }
            }
            #endregion
        }
        //求场馆与大厅交叉后，大厅需要减去的面积
        public void LobbyMinusIntersection()
        {
            #region 遍历标高为6的楼层，检查场馆与大厅相交情况
            if (StaticObject.buildingPerFloor[1] != null)
            {
                for (int j = 0; j < StaticObject.buildingPerFloor[1].Count; j++)
                {
                    if (StaticObject.buildingPerFloor[1][j] != null)
                    {
                        //监测该场馆是否与首层大厅相交
                        Curve lobbyTemp = StaticObject.baseBoundary[0].DuplicateCurve();
                        lobbyTemp.Transform(Transform.Translation(Vector3d.ZAxis * 6));
                        Curve buildingTemp = StaticObject.baseBoundary[j].DuplicateCurve();
                        CurveIntersections intersectionA = Intersection.CurveCurve(lobbyTemp, buildingTemp, StaticObject.accuracy2, StaticObject.accuracy2);
                        //若相交，则大厅扣减相应面积
                        if (intersectionA.Count > 1)
                        {
                            //求相交区域的轮廓线
                            Curve[] intersectionCurve = Curve.CreateBooleanIntersection(lobbyTemp, buildingTemp, StaticObject.accuracy2);
                            Curve[] boundaryCurve = Curve.JoinCurves(intersectionCurve, StaticObject.accuracy2, false);
                            //计算相交区域的面积
                            AreaMassProperties compute = AreaMassProperties.Compute(boundaryCurve);
                            if (compute != null)
                            {
                                StaticObject.areaActual[0][0] -= compute.Area;
                            }
                        }
                    }
                }
            }
            #endregion

            #region 遍历12米及以上的楼层，检查场馆与12米标高大厅相交情况
            for (int i = 2; i < StaticObject.buildingPerFloor.Count; i++)
            {
                if (StaticObject.buildingPerFloor[i] != null)
                {
                    for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                    {
                        if ((StaticObject.buildingPerFloor[i][j] != null) && (!((i == 2) && (j == 0))))//不是12米标高大厅
                        {
                            //监测该场馆是否与12米标高大厅大厅相交
                            Curve lobbyTemp = StaticObject.baseBoundaryUpFloor[2][0].DuplicateCurve();
                            lobbyTemp.Transform(Transform.Translation(Vector3d.ZAxis * 6 * (i - 2)));
                            Curve buildingTemp = StaticObject.baseBoundaryUpFloor[i][j].DuplicateCurve();
                            CurveIntersections intersectionA = Intersection.CurveCurve(lobbyTemp, buildingTemp, StaticObject.accuracy2, StaticObject.accuracy2);
                            //若相交，则大厅扣减相应面积
                            if (intersectionA.Count > 1)
                            {
                                //求相交区域的轮廓线
                                Curve[] intersectionCurve = Curve.CreateBooleanIntersection(lobbyTemp, buildingTemp, StaticObject.accuracy2);
                                Curve[] boundaryCurve = Curve.JoinCurves(intersectionCurve, StaticObject.accuracy2, false);
                                //计算相交区域的面积
                                AreaMassProperties compute = AreaMassProperties.Compute(boundaryCurve);
                                if (compute != null)
                                {
                                    StaticObject.areaActual[2][0] -= compute.Area;
                                }
                            }
                        }
                    }
                }
            }
            #endregion
        }
        //计算实际面积浮动率
        public void CalculateAreaFluctuationRate()
        {
            //数据重置
            StaticObject.areaRequiredTotal = 0;
            StaticObject.areaDeltaTotal = 0;
            StaticObject.areaFluctuationRate = 0;

            //任务书面积、差值面积累加
            for (int i = 0; i < StaticObject.areaRequired.Count; i++)
            {
                if (StaticObject.areaRequired[i] != null)
                {
                    for (int j = 0; j < StaticObject.areaRequired[i].Count; j++)
                    {
                        StaticObject.areaRequiredTotal += StaticObject.areaRequired[i][j];
                        StaticObject.areaDeltaTotal += StaticObject.areaDelta[i][j];
                    }
                }
            }

            //面积浮动率计算
            StaticObject.areaFluctuationRate = Math.Abs(StaticObject.areaDeltaTotal) / StaticObject.areaRequiredTotal;

        }
        //计算容积率
        public void GetBuildingDensity()
        {
            //遍历首层各场馆
            for (int i = 0; i < StaticObject.buildingPerFloor[0].Count; i++)
            {
                if (StaticObject.baseBoundary[i] != null)
                {
                    //计算各场馆底面积之和
                    AreaMassProperties compute = AreaMassProperties.Compute(StaticObject.baseBoundary[i]);
                    SiteInfo.groundFloorAreaActual += compute.Area;
                }
            }
            //计算实际容积率
            SiteInfo.buildingDensityActual = SiteInfo.groundFloorAreaActual / SiteInfo.siteArea;
        }
        //悬挑超出极限位置的面积占比
        public void CalculateOverLimitationArea()
        {
            #region 重置数据
            StaticObject.areaOverLimitation = 0;
            StaticObject.areaOverLimitationRate = 0;
            curveTemp = new List<Curve>();//【Delete】
            #endregion

            #region 解决之前非2层边界不准确的问题
            for (int i = 1; i < StaticObject.floorBoundary.Count; i++)
            {
                if (StaticObject.floorBoundary[i] != null)
                {
                    //重新求扩展边界交集，之前的计算有误
                    StaticObject.floorBoundaryUnion[i] = Curve.CreateBooleanUnion(StaticObject.floorBoundary[i], StaticObject.accuracy2);
                    foreach (var item in StaticObject.floorBoundaryUnion[i])
                    {
                        item.MakeClosed(StaticObject.accuracy2);
                    }
                }
            }
            #endregion

            #region 遍历各场馆
            for (int i = 1; i < StaticObject.buildingPerFloor.Count; i++)
            {
                if (StaticObject.buildingPerFloor[i] != null)
                {
                    for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                    {
                        if (StaticObject.buildingPerFloor[i][j] != null)
                        {
                            if (!((i == 2) && (j == 0)))//不是大厅
                            {
                                for (int k = 0; k < StaticObject.floorBoundaryUnion[i].Length; k++)
                                {
                                    //检测StaticObject.floorBoundaryUnion[i][k]是否包含StaticObject.baseBoundaryUpFloor[i][j],或毫无交集
                                    bool ifContain = IfCurveBContainsCurveA(StaticObject.baseBoundaryUpFloor[i][j], StaticObject.floorBoundaryUnion[i][k]);
                                    if (ifContain == false)//若StaticObject.floorBoundaryUnion[i][k]不包含StaticObject.baseBoundaryUpFloor[i][j]
                                    {
                                        //StaticObject.baseBoundary[j]减去该层的floorBoundaryUnion
                                        Curve[] intersectionCurve = Curve.CreateBooleanDifference(StaticObject.baseBoundaryUpFloor[i][j], StaticObject.floorBoundaryUnion[i], StaticObject.accuracy2);
                                        //检测是否所得曲线都属于StaticObject.baseBoundary[j]
                                        intersectionCurve = PurifyCurve(StaticObject.baseBoundaryUpFloor[i][j], intersectionCurve);
                                        //添加有效悬挑曲线至boundaryCurve，并计算面积
                                        if (intersectionCurve.Length != 0)
                                        {
                                            Curve[] boundaryCurve = Curve.JoinCurves(intersectionCurve, StaticObject.accuracy2, false);
                                            //【Delete】
                                            for (int m = 0; m < boundaryCurve.Length; m++)
                                            {
                                                curveTemp.Add(boundaryCurve[m]);
                                            }

                                            //计算多出悬挑范围的面积
                                            for (int m = 0; m < boundaryCurve.Length; m++)
                                            {
                                                AreaMassProperties compute1 = AreaMassProperties.Compute(boundaryCurve[m]);
                                                if (compute1 != null)
                                                {
                                                    StaticObject.areaOverLimitation += compute1.Area;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            StaticObject.areaOverLimitationRate = StaticObject.areaOverLimitation / StaticObject.areaRequiredTotal;
        }
        //曲线B是否包含曲线A,曲线A被曲线B包含时，或完全没有交集，不必做下一步差集运算，返回TRUE
        public bool IfCurveBContainsCurveA(Curve a, Curve b)
        {
            //获取曲线A的顶点
            List<Point3d> vertices = new List<Point3d>();
            vertices.Add(a.GetBoundingBox(true).Max);
            vertices.Add(a.GetBoundingBox(true).Min);
            vertices.Add(new Point3d(vertices[0].X, vertices[1].Y, vertices[0].Z));
            vertices.Add(new Point3d(vertices[1].X, vertices[0].Y, vertices[0].Z));

            //曲线B以外的点数量
            int outsideCount = 0;
            //求A曲线有多少个点在曲线B外
            for (int i = 0; i < vertices.Count; i++)
            {
                PointContainment pointContainment = b.Contains(vertices[i], new Plane(vertices[0], vertices[1], vertices[3]), StaticObject.accuracy2);

                if (pointContainment == PointContainment.Outside)
                {
                    outsideCount++;
                }
            }
            if ((outsideCount == 0) || (outsideCount == 4))//曲线A被曲线B包含时，或完全没有交集，不必做下一步差集运算，返回TRUE
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        //检测是否所得曲线都属于曲线A,删除不属于的
        public Curve[] PurifyCurve(Curve a, Curve[] boundaryCurve)
        {
            #region 数据准备
            //是否需要删除boundaryCurve中的某段曲线，因为它不属于曲线A
            List<bool> ifDelete = new List<bool>();
            //存放整理好的Curve
            List<Curve> boundaryCurveNew = new List<Curve>();
            #endregion

            #region 判断是否在曲线A中，不在则记录
            for (int i = 0; i < boundaryCurve.Length; i++)
            {
                //判断曲线A是否包含boundaryCurve[i]
                Curve[] intersectionCurve = Curve.CreateBooleanIntersection(a, boundaryCurve[i], StaticObject.accuracy2);
                //判断是否需要删除
                if (intersectionCurve.Length == 0)//A不包含，要删除
                {
                    ifDelete.Add(true);
                }
                else//A包含，不删除
                {
                    ifDelete.Add(false);
                }
            }
            #endregion

            #region 统计需删除的数量
            int count = 0;
            for (int i = 0; i < ifDelete.Count; i++)
            {
                if (ifDelete[i] == true)
                {
                    count++;
                }
            }
            #endregion

            #region 曲线清理
            if (count == 0)//若不需要清理曲线，则返回
            {
                return boundaryCurve;
            }
            else//需要清理曲线
            {
                //清理不要的曲线
                for (int i = 0; i < ifDelete.Count; i++)
                {
                    if (ifDelete[i] == false)
                    {
                        boundaryCurveNew.Add(boundaryCurve[i]);
                    }
                }
                boundaryCurve = new Curve[boundaryCurveNew.Count];
                for (int i = 0; i < boundaryCurveNew.Count; i++)
                {
                    boundaryCurve[i] = boundaryCurveNew[i];
                }
                return boundaryCurve;
            }
            #endregion
        }
        //计算首层布局紧凑程度
        public void GetCompactness()
        {
            #region 获取首层长褂布局的总轮廓线
            //获取首层场馆边界线并集
            Curve[] floorBoundary = new Curve[StaticObject.baseBoundary.Count];
            Curve[] floorBoundary2 = new Curve[StaticObject.baseBoundary.Count];//存放首层单体场馆轮廓扩处后的轮廓线  
            for (int i = 0; i < StaticObject.baseBoundary.Count; i++)
            {
                if (StaticObject.baseBoundary[i] != null)
                {
                    floorBoundary[i] = StaticObject.baseBoundary[i].DuplicateCurve();
                    floorBoundary[i].Transform(Transform.Translation(-Vector3d.ZAxis * floorBoundary[i].GetBoundingBox(false).Center.Z));
                    //单体外轮廓扩展，以更好地连接
                    floorBoundary2[i] = floorBoundary[i].Offset(Plane.WorldXY, StaticObject.offset, StaticObject.accuracy2, CurveOffsetCornerStyle.Sharp)[0];
                }
            }
            Curve[] newBoundary = Curve.CreateBooleanUnion(floorBoundary2, StaticObject.accuracy2);//求并集
            List<Curve[]> tempBoundary = new List<Curve[]>();//存放首层轮廓缩回的轮廓线    

            //构建首层边界曲线
            for (int i = 0; i < newBoundary.Length; i++)
            {
                if (newBoundary[i] != null)
                {
                    tempBoundary.Add(newBoundary[i].Offset(Plane.WorldXY, -StaticObject.offset, StaticObject.accuracy2, CurveOffsetCornerStyle.Sharp));
                }
            }
            //获取向首层外轮廓内部缩放的曲线边界
            Curve[] tempOffsetBoundary = new Curve[tempBoundary.Count];
            for (int i = 0; i < tempBoundary.Count; i++)
            {
                tempOffsetBoundary[i] = tempBoundary[i][0];
            }
            StaticObject.compactnessCurve = tempOffsetBoundary;
            #endregion

            #region 计算曲线面积
            //记录每条边界的面积，为判断是否包含做好准备
            List<double> areaTemp = new List<double>();
            //记录边界是计正面积还是负面积
            List<bool> ifAddArea = new List<bool>();
            //记录总面积
            double area = 0;
            //获取每条边界的面积
            for (int i = 0; i < tempOffsetBoundary.Length; i++)
            {
                if (tempOffsetBoundary[i] != null)
                {
                    tempOffsetBoundary[i].MakeClosed(StaticObject.accuracy2);//封闭曲线
                    AreaMassProperties compute = AreaMassProperties.Compute(tempOffsetBoundary[i]);
                    areaTemp.Add(compute.Area);
                }
            }
            //获取面积最大的索引号
            int max = 0;
            double maxArea = areaTemp[0];
            for (int i = 0; i < areaTemp.Count; i++)
            {
                if (areaTemp[i] > maxArea)
                {
                    maxArea = areaTemp[i];
                    max = i;
                }
            }
            //判断面积需要加还是减
            for (int i = 0; i < areaTemp.Count; i++)
            {
                if (max == i)//若为最大轮廓，则直接加
                {
                    ifAddArea.Add(true);
                }
                else//若非最大轮廓，验证其是否在最大轮廓内
                {

                    Point3d testPoint = tempOffsetBoundary[i].GetBoundingBox(true).Min;
                    PointContainment containment = tempOffsetBoundary[max].Contains(testPoint, Plane.WorldXY, StaticObject.accuracy2);
                    if (containment == PointContainment.Inside)//若在，则减去面积
                    {
                        ifAddArea.Add(false);
                    }
                    else//若不在，则叠加面积
                    {
                        ifAddArea.Add(true);
                    }
                }
            }
            //计算首层边界面积之和
            for (int i = 0; i < areaTemp.Count; i++)
            {
                if (ifAddArea[i] == true)
                {
                    area += areaTemp[i];
                }
                else
                {
                    area -= areaTemp[i];
                }
            }
            StaticObject.boundaryArea = area;
            #endregion

            #region 计算曲线边长
            double length = 0;
            for (int i = 0; i < tempOffsetBoundary.Length; i++)
            {
                length += tempOffsetBoundary[i].GetLength();
            }
            #endregion

            //获取边长/面积的比值
            StaticObject.lengthDivideArea = length / area;
            StaticObject.boundaryLength = length;
            StaticObject.boundaryArea = area;
        }
        //计算与大厅联通的场馆率
        public void CalculateConnectionRate()
        {
            //重置初始值
            StaticObject.connectLobbyCount = 0;
            StaticObject.buildingCount = 0;
            StaticObject.connectionRate = 0;

            #region 计算一共有多少个场馆
            StaticObject.ifConnectLobby = new List<List<bool>>();
            for (int i = 0; i < StaticObject.buildingPerFloor.Count; i++)
            {
                StaticObject.ifConnectLobby.Add(new List<bool>());
                if (StaticObject.buildingPerFloor[i] != null)
                {
                    for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                    {
                        if (StaticObject.buildingPerFloor[i][j] != null)
                        {
                            if (!(((i == 0) && (j == 0)) || ((i == 2) && (j == 0))))//不是大厅
                            {
                                //场馆数+1
                                StaticObject.buildingCount++;

                                if (i == 0)//0米标高
                                {
                                    //计算与首层大厅的相邻关系
                                    StaticObject.ifConnectLobby[i].Add(IfConnectToEachOther(StaticObject.baseBoundary[0], StaticObject.baseHalfX[0], StaticObject.baseHalfY[0], StaticObject.baseBoundary[j], StaticObject.baseHalfX[j], StaticObject.baseHalfY[j]));
                                }
                                else if (i == 1)//6米标高
                                {
                                    //首层大厅标高移至i层对应标高
                                    Curve tempCurve = StaticObject.baseBoundary[0];
                                    tempCurve.Transform(Transform.Translation(Vector3d.ZAxis * i * StaticObject.standardFloorHeight));
                                    //计算与首层大厅的相邻关系
                                    StaticObject.ifConnectLobby[i].Add(IfConnectToEachOther(tempCurve, StaticObject.baseHalfX[0], StaticObject.baseHalfY[0], StaticObject.baseBoundaryUpFloor[i][j], StaticObject.baseHalfXUpFloor[i][j], StaticObject.baseHalfYUpFloor[i][j]));
                                }
                                else if (i == 2)//12米标高
                                {
                                    //计算与二层大厅的相邻关系
                                    StaticObject.ifConnectLobby[i].Add(IfConnectToEachOther(StaticObject.baseBoundaryUpFloor[i][0], StaticObject.baseHalfXUpFloor[i][0], StaticObject.baseHalfYUpFloor[i][0], StaticObject.baseBoundaryUpFloor[i][j], StaticObject.baseHalfXUpFloor[i][j], StaticObject.baseHalfYUpFloor[i][j]));
                                }
                                else//12米以上标高
                                {
                                    //首层大厅标高移至i层对应标高
                                    Curve tempCurve = StaticObject.baseBoundaryUpFloor[2][0];
                                    tempCurve.Transform(Transform.Translation(Vector3d.ZAxis * (i - 2) * StaticObject.standardFloorHeight));
                                    //计算与二层大厅的相邻关系
                                    StaticObject.ifConnectLobby[i].Add(IfConnectToEachOther(tempCurve, StaticObject.baseHalfXUpFloor[2][0], StaticObject.baseHalfYUpFloor[2][0], StaticObject.baseBoundaryUpFloor[i][j], StaticObject.baseHalfXUpFloor[i][j], StaticObject.baseHalfYUpFloor[i][j]));
                                }
                            }
                            else
                            {
                                StaticObject.ifConnectLobby[i].Add(false);//大厅记为false的相邻关系
                            }
                        }
                    }
                }
            }
            #endregion

            #region 统计各场馆与大厅的连接情况
            for (int i = 0; i < StaticObject.ifConnectLobby.Count; i++)
            {
                if (StaticObject.ifConnectLobby[i] != null)
                {
                    for (int j = 0; j < StaticObject.ifConnectLobby[i].Count; j++)
                    {
                        //将与大厅连接的场馆计数
                        if (StaticObject.ifConnectLobby[i][j] != false)
                        {
                            StaticObject.connectLobbyCount++;
                        }
                    }
                }
            }
            #endregion

            //计算连接率
            double rate = StaticObject.connectLobbyCount / StaticObject.buildingCount;
            StaticObject.connectionRate = Math.Round(rate, 2);
        }
        //获取两个场馆间是否相邻
        public bool IfConnectToEachOther(Curve a, double halfXA, double halfYA, Curve b, double halfXB, double halfYB)
        {
            double minDistX = halfXA + halfXB + StaticObject.offset;//a/b场馆间视为连接状态的最小X轴距离
            double minDistY = halfYA + halfYB + StaticObject.offset;//a/b场馆间视为连接状态的最小Y轴距离
            double deltaX = Math.Abs(a.GetBoundingBox(true).Center.X - b.GetBoundingBox(true).Center.X);//a/b场馆间形心X轴间距
            double deltaY = Math.Abs(a.GetBoundingBox(true).Center.Y - b.GetBoundingBox(true).Center.Y);//a/b场馆间形心Y轴间距
            double distX = deltaX - minDistX;//a/b场馆间X轴实际间距
            double distY = deltaY - minDistY;//a/b场馆间Y轴实际间距

            if ((distX <= StaticObject.accuracy2) && (distY <= StaticObject.accuracy2))//a/b场馆间视为连接状态
            {
                return true;
            }
            else//a/b场馆间不能视为连接状态
            {
                return false;
            }
        }
        //判断游泳馆与滑冰馆是否相邻，场馆间间距不大于StaticObject.offset时视为相邻
        public void IfIceHockeyBuildingNearAquaticBuilding()
        {
            //重置数据
            StaticObject.ifEnergyConservation = false;

            #region 获取冰场所在楼层及场馆编号
            int iceHockeyLevel = StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[0].currentLevel;
            int iceHockeyIndex = StaticObject.iceHockeyBuilding.generalCourtBuildingGroups[0].itemIndex;
            Curve a;
            double halfXA;
            double halfYA;
            if (iceHockeyLevel == 0)//位于首层
            {
                a = StaticObject.baseBoundary[iceHockeyIndex].DuplicateCurve();
                halfXA = StaticObject.baseHalfX[iceHockeyIndex];
                halfYA = StaticObject.baseHalfY[iceHockeyIndex];
            }
            else//位于非首层
            {
                a = StaticObject.baseBoundaryUpFloor[iceHockeyLevel][iceHockeyIndex];
                halfXA = StaticObject.baseHalfXUpFloor[iceHockeyLevel][iceHockeyIndex];
                halfYA = StaticObject.baseHalfYUpFloor[iceHockeyLevel][iceHockeyIndex];
            }
            #endregion

            #region 获取游泳馆所在楼层及场馆编号
            List<int> aquaticLevel = new List<int>();
            List<int> aquaticIndex = new List<int>();
            List<Curve> b = new List<Curve>();
            List<double> halfXB = new List<double>();
            List<double> halfYB = new List<double>();
            for (int i = 0; i < StaticObject.aquaticBuilding.aquaticBuildingGroup.Count; i++)
            {
                aquaticLevel.Add(StaticObject.aquaticBuilding.aquaticBuildingGroup[i].currentLevel);
                aquaticIndex.Add(StaticObject.aquaticBuilding.aquaticBuildingGroup[i].itemIndex);
            }
            for (int i = 0; i < aquaticLevel.Count; i++)
            {
                if (aquaticLevel[i] == 0)//位于首层
                {
                    //与冰球馆统一标高
                    Curve temp = StaticObject.baseBoundary[aquaticIndex[i]].DuplicateCurve();
                    temp.Transform(Transform.Translation(Vector3d.ZAxis * (iceHockeyLevel - aquaticLevel[i]) * StaticObject.standardFloorHeight));
                    b.Add(temp);
                    //获取场馆尺度
                    halfXB.Add(StaticObject.baseHalfX[aquaticIndex[i]]);
                    halfYB.Add(StaticObject.baseHalfY[aquaticIndex[i]]);
                }
                else//位于非首层
                {
                    //与冰球馆统一标高
                    Curve temp = StaticObject.baseBoundaryUpFloor[aquaticLevel[i]][aquaticIndex[i]].DuplicateCurve();
                    temp.Transform(Transform.Translation(Vector3d.ZAxis * (iceHockeyLevel - aquaticLevel[i]) * StaticObject.standardFloorHeight));
                    b.Add(temp);
                    //获取场馆尺度
                    halfXB.Add(StaticObject.baseHalfXUpFloor[aquaticLevel[i]][aquaticIndex[i]]);
                    halfYB.Add(StaticObject.baseHalfYUpFloor[aquaticLevel[i]][aquaticIndex[i]]);
                }
            }
            #endregion

            #region 判断冰球与游泳馆位置关系
            for (int i = 0; i < aquaticLevel.Count; i++)
            {
                bool ifNearby = IfConnectToEachOther(a, halfXA, halfYA, b[i], halfXB[i], halfYB[i]);
                if (ifNearby)//挨着，则记录下来，直接返回
                {
                    StaticObject.ifEnergyConservation = true;
                    return;
                }
            }
            #endregion
        }
        //判断相同功能的相邻率
        public void GetSameFunctionAjoinRate()
        {
            //重置数据
            StaticObject.ifSplitBuilding = false;
            StaticObject.sameFunctionAdjoinRate = 0;
            double ajacentCount = 0;
            double buildingCount = 0;

            #region  按场馆判断相同功能的拆分情况
            if (StaticObject.aquaticBuilding.aquaticBuildingGroup.Count > 1)
            {
                StaticObject.ifSplitBuilding = true;
                //求是否相邻
                bool ifAjacent = IfSameFunctionAjoin("游泳馆");
                //获取涉及的场馆数
                for (int i = 0; i < StaticObject.aquaticBuilding.aquaticBuildingGroup.Count; i++)
                {
                    buildingCount++;
                    if (ifAjacent)//若相邻，则计入相邻场馆个数
                    {
                        ajacentCount++;
                    }
                }
            }

            if (StaticObject.badmintonBuilding.generalCourtBuildingGroups.Count > 1)
            {
                StaticObject.ifSplitBuilding = true;
                //求是否相邻
                bool ifAjacent = IfSameFunctionAjoin("羽毛球馆");
                //获取涉及的场馆数
                for (int i = 0; i < StaticObject.badmintonBuilding.generalCourtBuildingGroups.Count; i++)
                {
                    buildingCount++;
                    if (ifAjacent)//若相邻，则计入相邻场馆个数
                    {
                        ajacentCount++;
                    }
                }
            }

            if (StaticObject.tennisBuilding.generalCourtBuildingGroups.Count > 1)
            {
                StaticObject.ifSplitBuilding = true;
                //求是否相邻
                bool ifAjacent = IfSameFunctionAjoin("网球馆");
                //获取涉及的场馆数
                for (int i = 0; i < StaticObject.tennisBuilding.generalCourtBuildingGroups.Count; i++)
                {
                    buildingCount++;
                    if (ifAjacent)//若相邻，则计入相邻场馆个数
                    {
                        ajacentCount++;
                    }
                }
            }

            if (StaticObject.gymnasium.gymnasiumGroup.Count > 1)
            {
                StaticObject.ifSplitBuilding = true;
                //求是否相邻
                bool ifAjacent = IfSameFunctionAjoin("健身馆");
                //获取涉及的场馆数
                for (int i = 0; i < StaticObject.gymnasium.gymnasiumGroup.Count; i++)
                {
                    buildingCount++;
                    if (ifAjacent)//若相邻，则计入相邻场馆个数
                    {
                        ajacentCount++;
                    }
                }
            }
            #endregion

            //计算相邻率
            StaticObject.sameFunctionAdjoinRate = ajacentCount / buildingCount;
        }
        //判断同一种功能的2个场馆的位置关系是否相邻
        public bool IfSameFunctionAjoin(string function)
        {
            #region 变量
            int levelA = -1;
            int levelB = -1;
            int indexA = -1;
            int indexB = -1;
            Curve a;
            Curve b;
            double halfXA;
            double halfXB;
            double halfYA;
            double halfYB;
            #endregion

            #region 数据获取
            switch (function)
            {
                case "游泳馆":
                    levelA = StaticObject.aquaticBuilding.aquaticBuildingGroup[0].currentLevel;
                    levelB = StaticObject.aquaticBuilding.aquaticBuildingGroup[1].currentLevel;
                    indexA = StaticObject.aquaticBuilding.aquaticBuildingGroup[0].itemIndex;
                    indexB = StaticObject.aquaticBuilding.aquaticBuildingGroup[1].itemIndex;
                    break;
                case "羽毛球馆":
                    levelA = StaticObject.badmintonBuilding.generalCourtBuildingGroups[0].currentLevel;
                    levelB = StaticObject.badmintonBuilding.generalCourtBuildingGroups[1].currentLevel;
                    indexA = StaticObject.badmintonBuilding.generalCourtBuildingGroups[0].itemIndex;
                    indexB = StaticObject.badmintonBuilding.generalCourtBuildingGroups[1].itemIndex;
                    break;
                case "网球馆":
                    levelA = StaticObject.tennisBuilding.generalCourtBuildingGroups[0].currentLevel;
                    levelB = StaticObject.tennisBuilding.generalCourtBuildingGroups[1].currentLevel;
                    indexA = StaticObject.tennisBuilding.generalCourtBuildingGroups[0].itemIndex;
                    indexB = StaticObject.tennisBuilding.generalCourtBuildingGroups[1].itemIndex;
                    break;
                case "健身馆":
                    levelA = StaticObject.gymnasium.gymnasiumGroup[0].currentLevel;
                    levelB = StaticObject.gymnasium.gymnasiumGroup[1].currentLevel;
                    indexA = StaticObject.gymnasium.gymnasiumGroup[0].itemIndex;
                    indexB = StaticObject.gymnasium.gymnasiumGroup[1].itemIndex;
                    break;
                default:
                    break;
            }

            if (levelA == 0)
            {
                a = StaticObject.baseBoundary[indexA].DuplicateCurve();
                halfXA = StaticObject.baseHalfX[indexA];
                halfYA = StaticObject.baseHalfY[indexA];
            }
            else
            {
                a = StaticObject.baseBoundaryUpFloor[levelA][indexA].DuplicateCurve();
                halfXA = StaticObject.baseHalfXUpFloor[levelA][indexA];
                halfYA = StaticObject.baseHalfYUpFloor[levelA][indexA];
            }
            if (levelB == 0)
            {
                b = StaticObject.baseBoundary[indexB].DuplicateCurve();
                b.Transform(Transform.Translation(Vector3d.ZAxis * (levelB - levelA) * StaticObject.standardFloorHeight));
                halfXB = StaticObject.baseHalfX[indexB];
                halfYB = StaticObject.baseHalfY[indexB];
            }
            else
            {
                b = StaticObject.baseBoundaryUpFloor[levelB][indexB].DuplicateCurve();
                b.Transform(Transform.Translation(Vector3d.ZAxis * (levelB - levelA) * StaticObject.standardFloorHeight));
                halfXB = StaticObject.baseHalfXUpFloor[levelB][indexB];
                halfYB = StaticObject.baseHalfYUpFloor[levelB][indexB];
            }
            #endregion

            #region 判断是否相邻
            bool ifNearby = IfConnectToEachOther(a, halfXA, halfYA, b, halfXB, halfYB);
            if (ifNearby)//若相邻，返回true;反之，返回false
            {
                return true;
            }
            else
            {
                return false;
            }
            #endregion
        }
        //计算体型系数
        public void CalculateShapeFactor()
        {
            //重置数据
            StaticObject.volume = 0;
            StaticObject.superficialArea = 0;
            StaticObject.shapeFactor = 0;
            List<Brep> building = new List<Brep>();

            //获取所有场馆形体
            for (int i = 0; i < StaticObject.buildingPerFloor.Count; i++)
            {
                if (StaticObject.buildingPerFloor[i] != null)
                {
                    for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                    {
                        if (StaticObject.buildingPerFloor[i][j] != null)
                        {
                            building.Add(StaticObject.buildingPerFloor[i][j].GetBoundaryBox().Brep());
                        }
                    }
                }
            }

            //计算体积
            StaticObject.unionResult = Brep.CreateBooleanUnion(building, StaticObject.accuracy2);

            //计算表面积
            StaticObject.superficialArea = 0;
            if (StaticObject.unionResult != null)
            {
                for (int i = 0; i < StaticObject.unionResult.Length; i++)
                {
                    StaticObject.superficialArea += StaticObject.unionResult[i].GetArea();
                }
            }

            //计算体型系数
            StaticObject.volume = 0;
            if (StaticObject.unionResult != null)
            {
                for (int i = 0; i < StaticObject.unionResult.Length; i++)
                {
                    StaticObject.volume += StaticObject.unionResult[i].GetVolume();
                }
            }

            //计算体型系数
            StaticObject.shapeFactor = StaticObject.superficialArea / StaticObject.volume;
        }
        //写入Excel
        public void WriteExcelFile()
        {
            //数据准备
            string filePath = "C:\\Users\\Administrator\\Desktop";
            string fileName = "SpaceLayoutData.xlsx";
            string fileFullPath = Path.Combine(filePath, fileName);
            string fileFullPath2 = Path.Combine(filePath, "SpaceLayoutDataNew");
            //文件夹路径是否存在
            if (!Directory.Exists(filePath))//若不存在则新建目录
            {
                DirectoryInfo directoryInfo = Directory.CreateDirectory(filePath);
            }
            //若存在，则继续生成文件内容
            //检查excel文件是否存在
            if (!File.Exists(fileFullPath))//若不存在，则创建文件
            {
                IWorkbook myWorkbook = CreateFrame();
                using (FileStream fileStream = File.Create(fileFullPath))
                {
                    myWorkbook.Write(fileStream);
                    myWorkbook.Close();
                    myWorkbook.Dispose();
                }
            }
            else
            {

                XSSFWorkbook workbook= new XSSFWorkbook(fileFullPath);
                CreateContent(workbook);
                IWorkbook myWorkbook = workbook;
                using (FileStream fileStream = File.Create(fileFullPath2))
                {
                    myWorkbook.Write(fileStream);
                    myWorkbook.Close();
                    myWorkbook.Dispose();
                }
                //using (FileStream fileStream = File.OpenWrite(fileFullPath))
                //{
                //    myWorkbook.Write(fileStream);
                //    myWorkbook.Close();
                //    myWorkbook.Dispose();
                //}
            }
        }
        //创建excel的文件、表头
        public IWorkbook CreateFrame()
        {
            //创建工作簿
            XSSFWorkbook workBook = new XSSFWorkbook();
            //创建工作表
            string sheetName = "成功实例统计";
            ISheet sheet = workBook.CreateSheet(sheetName);
            //创建表头
            if (Manager.successTime == 1)
            {
                IRow titleRow = sheet.CreateRow(0);
                titleRow.CreateCell(0).SetCellValue("总序号");
                titleRow.CreateCell(1).SetCellValue("成功序号");
                titleRow.CreateCell(2).SetCellValue("建筑面积浮动率");
                titleRow.CreateCell(3).SetCellValue("建筑密度");
                titleRow.CreateCell(4).SetCellValue("体形系数");
                titleRow.CreateCell(5).SetCellValue("悬挑面积比"); 
                titleRow.CreateCell(6).SetCellValue("边长面积比");
                titleRow.CreateCell(7).SetCellValue("冰场与游泳馆贴临");
                titleRow.CreateCell(8).SetCellValue("大厅连接率");
                titleRow.CreateCell(9).SetCellValue("同功能贴临率");
                //格式调整
                for (int i = 0; i < 10; i++)
                {
                    sheet.AutoSizeColumn(i);
                }
            }
            //创建工作部内实质性内容
            CreateContent(workBook);
            return workBook;
        }
        //创建要输入excel的数据
        public void CreateContent(XSSFWorkbook workBook)
        {
            IRow row = workBook.GetSheetAt(0).CreateRow(Manager.successTime);
            row.CreateCell(0).SetCellValue(Manager.runningTime);
            row.CreateCell(1).SetCellValue(Manager.successTime);
            row.CreateCell(2).SetCellValue(StaticObject.areaFluctuationRate);
            row.CreateCell(3).SetCellValue(SiteInfo.buildingDensityActual);
            row.CreateCell(4).SetCellValue(StaticObject.shapeFactor);
            row.CreateCell(5).SetCellValue(StaticObject.areaOverLimitationRate);
            row.CreateCell(6).SetCellValue(StaticObject.lengthDivideArea);
            if (StaticObject.ifEnergyConservation == true)
            {
                row.CreateCell(7).SetCellValue(1);
            }
            else
            {
                row.CreateCell(7).SetCellValue(0);
            }
            row.CreateCell(8).SetCellValue(StaticObject.connectionRate);
            row.CreateCell(9).SetCellValue(StaticObject.sameFunctionAdjoinRate);
        }
        
        //变量
        List<Curve> curveTemp = new List<Curve>();

        protected override System.Drawing.Bitmap Icon => Properties.Resources._12_评估;

        public override Guid ComponentGuid
        {
            get { return new Guid("D474F128-4C17-46EE-A8EC-82308DB8BAD9"); }
        }
    }
}