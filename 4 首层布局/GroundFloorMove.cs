using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using Eto.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using static System.Net.Mime.MediaTypeNames;
using static Rhino.Render.TextureGraphInfo;
using static Space_Layout.Manager;

namespace Space_Layout
{
    //首层建筑单体移动
    public class GroundFloorMove : GH_Component
    {
        public GroundFloorMove()
          : base("首层建筑单体移动", "1F布局移动",
              "各场馆根据自身规则，向更优解移动",
               "建筑空间布局", "布局生成")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("上一步操作是否成功", "运行状态", "将首层建筑放入建筑退线是否成功", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("", "场馆中心点", "", GH_ParamAccess.list);
            pManager.AddBoxParameter("", "场馆单体", "", GH_ParamAccess.list);
            pManager.AddRectangleParameter("", "运动场地", "", GH_ParamAccess.list);
            pManager.AddBoxParameter("", "辅助用房", "", GH_ParamAccess.list);
            pManager.AddBooleanParameter("首层布局是否完成", "运行状态", "", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //生成状态，用于进程管理
            int isSuccess = 9;
            //用于计算移动
            List<List<Vector3d>> vectorPush = new List<List<Vector3d>>();//各场馆所有推力变量
            List<List<Vector3d>> vectorPull = new List<List<Vector3d>>();//各场馆所有拉力变量
            List<Vector3d> vectorPullTotal = new List<Vector3d>();//各场馆拉力汇总
            List<Vector3d> vectorTotal = new List<Vector3d>();//各场馆汇总后移动变量
            List<List<int>> position = new List<List<int>>();//各场馆相对位置变量（i围绕j）,-1不相邻，0=正右，1=正下，2=正左，3=正上
            List<List<int>> adjacent = new List<List<int>>();//各场馆毗邻关系（i围绕j）,-1不相邻，0=正右，1=正下，2=正左，3=正上
            for (int i = 0; i < StaticObject.floorCount; i++)//占位
            {
                vectorPush.Add(new List<Vector3d>());
                vectorPull.Add(new List<Vector3d>());
                vectorPullTotal.Add(new Vector3d());
                vectorTotal.Add(new Vector3d());
                position.Add(new List<int>());
                adjacent.Add(new List<int>());
                if (Manager.calculationTimes == 0)//服务于判定推力是否将首层体量摊开
                {
                    Manager.moveSum.Add(Vector3d.Zero);
                }
            }

            if (!DA.GetData(0, ref isSuccess)) return;

            if (isSuccess == 1)//若上一步成功
            {
                //首层大厅确定点位；
                if (!StaticObject.ifGroundLobbyFindLocation)
                {
                    AdjustGroundFloorLobby();
                    StaticObject.ifGroundLobbyFindLocation = true;
                }
                //首层场馆单体移动
                GetGroundFloorVector(StaticObject.buildingPerFloor[0], vectorPush, vectorTotal, vectorPull, position, adjacent, vectorPullTotal);
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    StaticObject.buildingPerFloor[0][i].Trans(Transform.Translation(vectorTotal[i]));
                }
                UpdateCenterPoint(vectorTotal);
                //累加运算次数，用于解决首层推力无法分开单体的问题
                if (Manager.ifBeginCalculate)
                {
                    Manager.calculationTimes++;
                }
                //累加运算次数，用于解决首层大厅吸引时的若干BUG
                if (Manager.ifPullCalculate)
                {
                    Manager.pullTimes++;
                }
            }

            //数据输出
            DA.SetDataList(0, StaticObject.baseCenter);
            DA.SetDataList(1, StaticObject.buildings);
            DA.SetDataList(2, StaticObject.courtR);
            DA.SetDataList(3, StaticObject.auxiliary);
            if (Manager.ifBegin2F)
            {
                DA.SetData(4, Manager.ifBegin2F);
            }
        }
        //获取首层建筑单体移动
        public void GetGroundFloorVector(List<ITrans> input, List<List<Vector3d>> vectorPush, List<Vector3d> vectorTotal, List<List<Vector3d>> vectorPull, List<List<int>> position, List<List<int>> adjacent, List<Vector3d> vectorPullTotal)
        {
            #region 数据准备
            //获取各建筑单体相对位置
            GetBuildingPosition(StaticObject.baseOffset, position);
            //更新次数统计开关,一旦进入shrink状态，不再进行推力作用计数
            if (!Manager.ifStop)
            {
                Manager.ifBeginCalculate = true;
            }
            //计算各单体分项受力
            for (int i = 0; i < StaticObject.floorCount; i++)
            {
                for (int j = 0; j < StaticObject.floorCount; j++)
                {
                    if (i != j)
                    {
                        if ((input[i] is BasketballMatchBuilding) && ((!(input[j] is Office)) && (!(input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, StaticObject.baseOffset));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, StaticObject.baseOffset, position[i], adjacent[i]));
                        }
                        else if ((input[i] is BasketballMatchBuilding) && (((input[j] is Office)) || ((input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else if ((input[i] is GeneralCourtBuildingGroup) && ((!(input[j] is Office)) && (!(input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, StaticObject.baseOffset));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, StaticObject.baseOffset, position[i], adjacent[i]));
                        }
                        else if ((input[i] is GeneralCourtBuildingGroup) && (((input[j] is Office)) || ((input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else if ((input[i] is AquaticBuildingGroup) && ((!(input[j] is Office)) && (!(input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, StaticObject.baseOffset));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, StaticObject.baseOffset, position[i], adjacent[i]));
                        }
                        else if ((input[i] is AquaticBuildingGroup) && (((input[j] is Office)) || ((input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else if ((input[i] is GymnasiumGroup) && ((!(input[j] is Office)) && (!(input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, StaticObject.baseOffset));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, StaticObject.baseOffset, position[i], adjacent[i]));
                        }
                        else if ((input[i] is GymnasiumGroup) && (((input[j] is Office)) || ((input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else if ((input[i] is Theater) && ((!(input[j] is Office)) && (!(input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, StaticObject.baseOffset));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, StaticObject.baseOffset, position[i], adjacent[i]));
                        }
                        else if ((input[i] is Theater) && (((input[j] is Office)) || ((input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else if ((input[i] is OtherFunction) && ((!(input[j] is Office)) && (!(input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, StaticObject.baseOffset));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, StaticObject.baseOffset, position[i], adjacent[i]));
                        }
                        else if ((input[i] is OtherFunction) && (((input[j] is Office)) || ((input[j] is LobbyUnit))))
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else if (input[i] is LobbyUnit)
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else if (input[i] is Office)
                        {
                            vectorPush[i].Add(GetOverlapVector(input[i], i, input[j], j, 0));
                            vectorPull[i].Add(GetPullVector(input[i], i, input[j], j, 0, position[i], adjacent[i]));
                        }
                        else
                        {
                            vectorPush[i].Add(Vector3d.Zero);
                            vectorPull[i].Add(Vector3d.Zero);
                            adjacent[i].Add(-1);
                            position[i].Add(-1);
                        }
                    }
                    else
                    {
                        vectorPush[i].Add(Vector3d.Zero);
                        vectorPull[i].Add(Vector3d.Zero);
                        adjacent[i].Add(-1);
                    }
                }
            }
            #endregion

            #region 汇总所得移动向量为每个场馆单一移动向量
            for (int i = 0; i < StaticObject.floorCount; i++)
            {
                #region 汇总推力
                for (int j = 0; j < vectorPush[i].Count; j++)
                {
                    if (j == 0)//vectorTotal的0号索引，直接复制
                    {
                        vectorTotal[i] += vectorPush[i][j];
                    }
                    else// vectorTotal【索引号】有值了
                    {
                        double x = 0;
                        double y = 0;
                        if (vectorTotal[i].X * vectorPush[i][j].X > 0)//X轴有同向力，则选大的
                        {
                            if (Math.Abs(vectorTotal[i].X) < Math.Abs(vectorPush[i][j].X))
                            {
                                x = vectorPush[i][j].X;
                            }
                            else
                            {
                                x = vectorTotal[i].X;
                            }
                        }
                        else//X轴异向或一方为0，则相加
                        {
                            x = vectorTotal[i].X + vectorPush[i][j].X;
                        }
                        if (vectorTotal[i].Y * vectorPush[i][j].Y > 0)//Y轴有同向力，则选大的
                        {
                            if (Math.Abs(vectorTotal[i].Y) < Math.Abs(vectorPush[i][j].Y))
                            {
                                y = vectorPush[i][j].Y;
                            }
                            else
                            {
                                y = vectorTotal[i].Y;
                            }
                        }
                        else//Y轴异向或一方为0，则相加
                        {
                            y = vectorTotal[i].Y + vectorPush[i][j].Y;
                        }
                        vectorTotal[i] = new Vector3d(x, y, 0);
                    }
                }
                #endregion

                #region 汇总吸引力
                //if (vectorTotal[i].Length == 0)
                //{
                //    vectorPullTotal[i] = FilterPullVector(vectorPull[i], adjacent[i], position[i]);
                //    vectorTotal[i] += vectorPullTotal[i];
                //}
                #endregion

                #region 判断是否出界，若出界拉回来
                if (IfMoveOverBoundary(SiteInfo.siteRetreat.Value, StaticObject.baseBoundary[i], vectorTotal[i]).Length != 0)
                {
                    vectorTotal[i] = IfMoveOverBoundary(SiteInfo.siteRetreat.Value, StaticObject.baseBoundary[i], vectorTotal[i]);
                }
                #endregion
            }
            #endregion

            #region 解决推力无法散开体块的问题
            //查看是否推力都为零了
            bool isPushZero = true;
            bool isPullBegin = false;
            for (int i = 0; i < StaticObject.floorCount; i++)//检查是否存在推力
            {
                if (vectorTotal[i].Length != 0)
                {
                    isPushZero = false;
                }
            }
            //进入下一阶段
            if (isPushZero) isPullBegin = true;
            //存在推力且超出推力扩散忍耐次数限值
            if ((!isPullBegin) && ((Manager.calculationTimes > 150) && (Manager.calculationTimes <= 170)))
            {
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    Manager.moveSum[i] += vectorTotal[i];
                }
            }
            //超出推力累加限值时，旋转
            if ((Manager.calculationTimes == 171) && (!isPullBegin))
            {
                double sum = 0;
                int count = 0;
                double average = 0;
                //求平均受力大小
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    sum += Manager.moveSum[i].Length;
                    if (Manager.moveSum[i].Length != 0)
                    {
                        count++;
                    }
                }
                average = sum / count;
                //当确定为没有散开的情况，查找还有受力的对象，不与基地平行的挑位于中间的旋转
                if ((average < 35) && (average > 0))
                {
                    List<int> isParrallel = new List<int>();//查找没有散开且与基底不平行的对象
                    List<double> distanceToSiteCenter = new List<double>();//查找没有散开且与基底不平行的对象
                    double largeAmount = 999999;//不参与计算的代表很大的数值
                    //找到与基底不平行的
                    for (int i = 0; i < StaticObject.floorCount; i++)
                    {
                        if (vectorTotal[i].Length != 0)//有受力
                        {
                            if (StaticObject.buildingPerFloor[0][i].Orientation != SiteInfo.siteOrientation)//朝向与基底基地垂直
                            {
                                isParrallel.Add(1);
                                //求到基地中心的距离
                                distanceToSiteCenter.Add((StaticObject.baseCenter[i] - SiteInfo.siteBoundary.Boundingbox.Center).Length);
                            }
                            else//朝向与基地相同
                            {
                                isParrallel.Add(2);
                                distanceToSiteCenter.Add(largeAmount);
                            }
                        }
                        else//无受力
                        {
                            isParrallel.Add(0);
                            distanceToSiteCenter.Add(largeAmount);
                        }
                    }
                    //获取距离基地中心最近的受力对象
                    double distanceShorter = largeAmount;//逐步替换为更小值
                    for (int i = 0; i < StaticObject.floorCount; i++)
                    {
                        if (distanceShorter > distanceToSiteCenter[i])
                        {
                            distanceShorter = distanceToSiteCenter[i];//找到更小的就替换
                        }
                    }
                    if (distanceShorter != largeAmount)//有垂直的情况
                    {
                        //锁定要旋转的对象
                        int index = distanceToSiteCenter.IndexOf(distanceShorter);
                        //旋转
                        StaticObject.buildingPerFloor[0][index].MustRotate(Math.PI * 90 / 180);
                        //更新数据
                        double tempHalf = StaticObject.baseHalfX[index];
                        StaticObject.baseHalfX[index] = StaticObject.baseHalfY[index];
                        StaticObject.baseHalfY[index] = tempHalf;
                        StaticObject.buildingPerFloor[0][index].SetOrientation();
                    }
                    else//没有垂直情况
                    {
                        //承接要移到中心的对象
                        int index;
                        //搜索要移到中心的对象
                        for (int i = 0; i < StaticObject.floorCount; i++)
                        {
                            if (isParrallel[i] == 2)
                            {
                                //移动至基地中心
                                Vector3d moveToCenter = SiteInfo.siteRetreat.Boundingbox.Center - StaticObject.baseCenter[i];
                                StaticObject.buildingPerFloor[0][i].Trans(Transform.Translation(moveToCenter));
                                StaticObject.baseCenter[i] = StaticObject.baseBoundary[i].GetBoundingBox(true).Center;
                                break;
                            }
                        }
                    }
                    //数据初始化
                    Manager.calculationTimes = 0;
                    for (int i = 0; i < StaticObject.floorCount; i++)
                    {
                        Manager.moveSum[i] += vectorTotal[i];
                    }
                }
            }
            #endregion

            #region 场馆被大厅吸引
            bool isNotNearMoveBegin = false;
            if (isPullBegin)
            {
                //数据初始化
                Manager.ifBeginCalculate = false;
                Manager.calculationTimes = 0;
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    Manager.moveSum[i] += vectorTotal[i];
                }
                //场馆被大厅吸引
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    //检查场馆i是否与大厅相邻,若不相邻，检查向大厅移动的方向是否有更近的遮挡物
                    if ((adjacent[i][0] == -1) && (i != 0))
                    {
                        int lobbyPosition = position[i][0];//获取大厅相对位置
                        if (!IfHasBarrierToLobby(position[i], lobbyPosition, i, vectorPull[i]))//若没有障碍物，则向着大厅移动
                        {
                            vectorTotal[i] = vectorPull[i][0];
                        }
                    }
                }
                //若达到平衡状态，不向大厅移动了，则此阶段完成，进入下一阶段
                double tempSum = 0;
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    tempSum += vectorTotal[i].Length;
                }
                if (tempSum == 0)
                {
                    isNotNearMoveBegin = true;
                }
            }
            #endregion

            #region 与大厅不相邻但尽可能靠近
            bool isLobbyMoveBegin = false;
            List<int> nearestBuilding = new List<int>();
            for (int i = 0; i < StaticObject.floorCount; i++)
            {
                nearestBuilding.Add(-1);
            }
            if ((isNotNearMoveBegin) && (!Manager.ifStop))
            {
                Manager.ifBeginCalculate = false;
                Manager.ifPullCalculate = true;
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    //检查场馆i是否与大厅相邻,若不相邻，检查向大厅移动的方向是否有更近的遮挡物
                    if ((adjacent[i][0] == -1) && (i != 0))
                    {
                        int lobbyPosition = position[i][0];//获取大厅相对位置
                        if (IfHasBarrierToLobby(position[i], lobbyPosition, i, vectorPull[i]))//若向着大厅有障碍物
                        {
                            int nearestIndex = GetNearestBuildingInSpecificOrientation(i, lobbyPosition, position[i]);//获取大厅方向可以前进的最小距离
                            nearestBuilding[i] = nearestIndex;
                            double distance;
                            double ratio = 0.1;
                            if (lobbyPosition == 3)
                            {
                                distance = GetDistanceBetweenTwoBuilding(i, nearestIndex, 3, 1);
                                if ((distance < 0.5) && (distance > 0))
                                {
                                    vectorTotal[i] = new Vector3d(0, -distance, 0);
                                }
                                else
                                {
                                    vectorTotal[i] = new Vector3d(0, -distance * ratio, 0);
                                }
                            }
                            else if (lobbyPosition == 1)
                            {
                                distance = GetDistanceBetweenTwoBuilding(i, nearestIndex, 1, 1);
                                if ((distance < 0.5) && (distance > 0))
                                {
                                    vectorTotal[i] = new Vector3d(0, distance, 0);
                                }
                                else
                                {
                                    vectorTotal[i] = new Vector3d(0, distance * ratio, 0);
                                }
                            }
                            else if (lobbyPosition == 2)
                            {
                                distance = GetDistanceBetweenTwoBuilding(i, nearestIndex, 2, 0);
                                if ((distance < 0.5) && (distance > 0))
                                {
                                    vectorTotal[i] = new Vector3d(distance, 0, 0);
                                }
                                else
                                {
                                    vectorTotal[i] = new Vector3d(distance * ratio, 0, 0);
                                }
                            }
                            else if (lobbyPosition == 0)
                            {
                                distance = GetDistanceBetweenTwoBuilding(i, nearestIndex, 0, 0);
                                if ((distance < 0.5) && (distance > 0))
                                {
                                    vectorTotal[i] = new Vector3d(-distance, 0, 0);
                                }
                                else if (distance >= 0.5)
                                {
                                    vectorTotal[i] = new Vector3d(-distance * ratio, 0, 0);
                                }
                            }
                            else
                            {

                            }
                        }
                    }
                }
                //查看朝向的移动对象是否正在移动，若是，则本场馆暂时不动
                for (int i = 0; i < StaticObject.floorCount; i++)
                {
                    if (nearestBuilding[i] != -1)//索引不超边界
                    {
                        if (vectorTotal[nearestBuilding[i]].Length != 0)//检测朝向移动的对象是否正在 运动
                        {
                            vectorTotal[i] = Vector3d.Zero;
                        }
                    }
                }
                //若达到平衡状态，不向大厅移动了，则此阶段完成，进入下一阶段
                double tempSum = 0;
                for (int j = 0; j < StaticObject.floorCount; j++)
                {
                    tempSum += vectorTotal[j].Length;
                }
                if (tempSum == 0)
                {
                    isLobbyMoveBegin = true;
                }
            }
            #endregion

            #region 大厅优化位置，向旁边但不贴临的场馆移动
            if ((isLobbyMoveBegin) && (!Manager.ifStop))
            {
                #region 记录大厅四周的毗邻情况
                bool up = false;//大厅在对象上
                bool down = false;//大厅在对象下
                bool left = false;//大厅在对象左
                bool right = false;//大厅在对象右
                                   //检测大厅四周的毗邻情况
                right = GetAdjacentBuilding(0, adjacent[0]); //大厅在对象右
                down = GetAdjacentBuilding(1, adjacent[0]);//大厅在对象下
                left = GetAdjacentBuilding(2, adjacent[0]);//大厅在对象左
                up = GetAdjacentBuilding(3, adjacent[0]);//大厅在对象上
                #endregion

                #region 大厅不毗邻的方向是否有东西，有的话是否能移动过去
                bool goNext = true;
                if (!right)//大厅左侧无毗邻
                {
                    if (GetNearestBuildingInSpecificOrientation(0, 0, position[0]) != -1)
                    {
                        vectorTotal[0] = vectorPull[0][GetNearestBuildingInSpecificOrientation(0, 0, position[0])];
                        goNext = false;
                    }
                }
                if ((!down) && goNext)//大厅上侧无毗邻
                {
                    if (GetNearestBuildingInSpecificOrientation(0, 1, position[0]) != -1)
                    {
                        vectorTotal[0] = vectorPull[0][GetNearestBuildingInSpecificOrientation(0, 1, position[0])];
                        goNext = false;
                    }
                }
                if ((!left) && goNext)//大厅右侧无毗邻
                {
                    int index = GetNearestBuildingInSpecificOrientation(0, 2, position[0]);
                    if (GetNearestBuildingInSpecificOrientation(0, 2, position[0]) != -1)
                    {
                        vectorTotal[0] = vectorPull[0][index];
                        goNext = false;
                    }
                }
                if ((!up) && goNext)//大厅下侧无毗邻
                {
                    if (GetNearestBuildingInSpecificOrientation(0, 3, position[0]) != -1)
                    {
                        vectorTotal[0] = vectorPull[0][GetNearestBuildingInSpecificOrientation(0, 3, position[0])];
                    }
                }
                #endregion

                #region 判断是否大厅三面贴临并向第四面移动，且永久往复
                int upIndex = GetNearestBuildingInSpecificOrientation(0, 1, position[0]);//获取大厅下侧最近对象
                int downIndex = GetNearestBuildingInSpecificOrientation(0, 3, position[0]);//获取大厅上侧最近对象
                int leftIndex = GetNearestBuildingInSpecificOrientation(0, 2, position[0]);//获取大厅右侧最近对象
                int rightIndex = GetNearestBuildingInSpecificOrientation(0, 0, position[0]);//获取大厅左侧最近对象
                double upDist = 0;//存放大厅下侧理论距离（与运动方向垂直）
                double downDist = 0;//存放大厅上侧理论距离（与运动方向垂直）
                double leftDist = 0;//存放大厅右侧理论距离（与运动方向垂直）
                double rightDist = 0;//存放大厅左侧理论距离（与运动方向垂直）

                if ((vectorTotal[0].X != 0) && (leftIndex != -1) && (rightIndex != -1) && ((upIndex != -1) || (downIndex != -1)))//大厅水平有受力且东西两侧有场馆
                {
                    double distanceActual = GetDistanceBetweenTwoBuilding(leftIndex, rightIndex, 0, 0);//获取大厅东西最近对象间距
                    if (upIndex != -1)//获取大厅下侧理论最小距离
                    {
                        upDist = StaticObject.baseHalfX[upIndex] * 2 + StaticObject.baseOffset;
                    }
                    if (downIndex != -1)//获取大厅下侧理论最小距离
                    {
                        downDist = StaticObject.baseHalfX[downIndex] * 2 + StaticObject.baseOffset;
                    }
                    if ((distanceActual == upDist) || (distanceActual == downDist))//会出现活塞运动情况
                    {
                        vectorTotal[0] = Vector3d.Zero;//大厅不动
                    }
                }
                else if ((vectorTotal[0].Y != 0) && (upIndex != -1) && (downIndex != -1) && ((leftIndex != -1) || (rightIndex != -1)))//大厅垂直有受力且南北两侧有场馆
                {
                    double distanceActual = GetDistanceBetweenTwoBuilding(upIndex, downIndex, 1, 1);//获取大厅南北最近对象间距
                    if (leftIndex != -1)//获取大厅右侧理论最小距离
                    {
                        leftDist = StaticObject.baseHalfY[leftIndex] * 2 + StaticObject.baseOffset;
                    }
                    if (rightIndex != -1)//获取大厅左侧理论最小距离
                    {
                        rightDist = StaticObject.baseHalfY[rightIndex] * 2 + StaticObject.baseOffset;
                    }
                    if ((distanceActual == leftDist) || (distanceActual == rightIndex))//会出现活塞运动情况
                    {
                        vectorTotal[0] = Vector3d.Zero;//大厅不动
                    }
                }
                #endregion

                #region 判断是否出界，若出界拉回来
                if (IfMoveOverBoundary(SiteInfo.siteRetreat.Value, StaticObject.baseBoundary[0], vectorTotal[0]).Length != 0)
                {
                    vectorTotal[0] = IfMoveOverBoundary(SiteInfo.siteRetreat.Value, StaticObject.baseBoundary[0], vectorTotal[0]);
                }
                #endregion
            }
            #endregion

            #region 开始场馆收缩
            #region 开关数据准备
            bool isBeginShrink = false;//控制开始布局收缩
            if ((Manager.pullTimes > StaticObject.pullTimeChange) || ((SumVector(vectorTotal) == 0) && (isLobbyMoveBegin)))//若超过运算次数或合力为0，开启收缩阶段
            {
                for (int j = 0; j < vectorTotal.Count; j++)
                {
                    vectorTotal[j] = Vector3d.Zero;//超过次数就不要再移动了
                }
                Manager.ifStop = true;//甩BUG开关
                isBeginShrink = true;
            }
            #endregion

            if (isBeginShrink)//收缩布局开始
            {
                #region 首次数据准备
                if (!Manager.ifShrinkPrepareOK)//首次数据准备
                {
                    List<bool> isUsed = new List<bool>();//用于判断是否所有场馆都添加到shrink列表里了
                    for (int i = 0; i < StaticObject.floorCount; i++)
                    {
                        isUsed.Add(false);
                        StaticObject.shrinkStatus.Add(false);
                    }

                    //录入大厅
                    StaticObject.shrinkOrder.Add(0);//大厅
                    isUsed[0] = true;//大厅录入完成

                    //录入与大厅相邻的场馆
                    List<int> adjacentIndex = GetAdjacentBuilding(adjacent[0]);
                    if (adjacentIndex.Count != 0)
                    {
                        foreach (var item in adjacentIndex)
                        {
                            StaticObject.shrinkOrder.Add(item);//该场馆录入shrink排序
                            isUsed[item] = true;//该场馆录入完成
                        }
                    }
                    //录入与大厅不相邻的场馆
                    if (HowManyBool(true, isUsed) < StaticObject.floorCount)
                    {
                        for (int i = 0; i < StaticObject.floorCount; i++)
                        {
                            if (isUsed[i] == false)
                            {
                                StaticObject.shrinkOrder.Add(i);//该场馆录入shrink排序
                                isUsed[i] = true;//该场馆录入完成
                            }
                        }
                    }
                    Manager.ifShrinkPrepareOK = true;//首次赋值完成
                }
                #endregion

                #region 收缩运动
                //求接近大厅中央可移动的X轴、Y轴变量
                if (Manager.shrinkCount < 10)//反复计算次数
                {
                    //更改进入计算的条件
                    StaticObject.pullTimeChange = 1;
                    //获取移动向量
                    vectorTotal[StaticObject.shrinkOrder[Manager.shrinkIndex]] = GetShrinkVector(StaticObject.shrinkOrder[Manager.shrinkIndex], position[StaticObject.shrinkOrder[Manager.shrinkIndex]]);
                    //计数器更新
                    if (Manager.shrinkIndex == (StaticObject.floorCount - 1))//新的一大轮开始
                    {
                        Manager.shrinkIndex = 1;
                        Manager.shrinkCount++;
                    }
                    else//本轮序号+1
                    {
                        Manager.shrinkIndex++;
                    }
                }
                if (Manager.shrinkCount == 10)
                {
                    Manager.ifShrinkOk = true;
                    Manager.ifBeginCalculate = false;
                    Manager.ifPullCalculate = false;
                }
                #endregion
            }
            #endregion

            #region 上层边界整理
            if ((Manager.ifShrinkOk) && (!Manager.ifBegin2F))
            {
                //union里添加首层边界，占位用
                StaticObject.floorBoundaryUnion = new List<Curve[]>();
                StaticObject.floorBoundaryUnion.Add(StaticObject.floorBoundary[0]);
                //合并各层的曲线
                for (int i = 1; i < StaticObject.floorBoundary.Count; i++)
                {
                    if (StaticObject.floorBoundary[i] != null)
                    {
                        if (i == 2)//确保12米标高一致，排除无法合并的一个理由
                        {
                            for (int j = 0; j < StaticObject.floorBoundary[i].Length; j++)
                            {
                                if (StaticObject.floorBoundary[i][j] != null)
                                {
                                    Point3d tempCenter = StaticObject.floorBoundary[i][j].GetBoundingBox(true).Center;
                                    Vector3d tempVector = new Point3d(tempCenter.X, tempCenter.Y, 12) - tempCenter;
                                    if (Math.Abs(tempVector.Z) > 2)//健身馆，未防止其ceiling飞出去，降低1个层高
                                    {
                                        tempVector -= Vector3d.ZAxis * StaticObject.standardFloorHeight;
                                    }
                                    StaticObject.floorBoundary[i][j].Transform(Transform.Translation(tempVector));
                                }
                            }
                        }
                        Curve[] tempUnion = Curve.CreateBooleanUnion(StaticObject.floorBoundary[i], 1);
                        if (tempUnion.Length >= 1)//合并成功
                        {
                            StaticObject.floorBoundaryUnion.Add(tempUnion);
                        }
                        else//无法合并成功
                        {
                            StaticObject.floorBoundaryUnion.Add(StaticObject.floorBoundary[i]);
                        }
                    }
                    else
                    {
                        StaticObject.floorBoundaryUnion.Add(null);
                    }
                }
                //减去篮球馆对首层大厅的影响
                for (int i = 1; i < adjacent[0].Count; i++)//求12米标高大厅轮廓与篮球馆边界差集
                {
                    if (adjacent[0][i] != -1)
                    {
                        if ((StaticObject.buildingTypes[i] == BuildingType.篮球训练馆) || (StaticObject.buildingTypes[i] == BuildingType.篮球比赛馆))
                        {
                            //复制要减去的对象底部边界
                            Curve tempLobbyBoundary = StaticObject.baseBoundary[i].DuplicateCurve();
                            //构建对象移动向量并移动
                            Vector3d tempMove = new Vector3d(0, 0, 12);
                            tempLobbyBoundary.Transform(Transform.Translation(tempMove));
                            //存储差集Curve
                            List<Curve> tempBoundary = new List<Curve>();
                            //求12米标高大厅轮廓与篮球馆边界差集
                            for (int j = 0; j < StaticObject.floorBoundaryUnion[2].Length; j++)
                            {
                                if (StaticObject.floorBoundaryUnion[2][j] != null)
                                {
                                    //求差集曲线
                                    Curve[] tempDifference = Curve.CreateBooleanDifference(StaticObject.floorBoundaryUnion[2][j], tempLobbyBoundary, 0.1);
                                    if (tempDifference.Length >= 1)//大厅与对象有相交
                                    {
                                        tempBoundary.Add(tempDifference[0]);
                                    }
                                    else//无相交
                                    {
                                        tempBoundary.Add(StaticObject.floorBoundaryUnion[2][j]);
                                    }
                                }
                            }
                            //重新赋值  
                            StaticObject.floorBoundaryUnion[2] = new Curve[tempBoundary.Count];
                            for (int q = 0; q < tempBoundary.Count; q++)//赋值
                            {
                                StaticObject.floorBoundaryUnion[2][q] = tempBoundary[q];
                            }
                        }
                    }
                }
                //减去基地退线
                for (int j = 1; j < StaticObject.floorBoundaryUnion.Count; j++)
                {
                    if (StaticObject.floorBoundaryUnion[j] != null)
                    {
                        for (int k = 0; k < StaticObject.floorBoundaryUnion[j].Length; k++)
                        {
                            if (StaticObject.floorBoundaryUnion[j][k] != null)
                            {
                                Curve tempCurve = SiteInfo.siteRetreat.DuplicateCurve().Value;
                                tempCurve.Transform(Transform.Translation(Vector3d.ZAxis * j * 6));
                                Curve[] result = Curve.CreateBooleanIntersection(StaticObject.floorBoundaryUnion[j][k], tempCurve, 0.1);
                                if (result.Length > 0)//求与基地退线差集成功，更新边界
                                {
                                    StaticObject.floorBoundaryUnion[j][k] = result[0];
                                }
                            }
                        }
                    }
                }

                //数据同步
                #region
                Manager.ifBegin2F = true;
                Manager.currentFloor = 1;
                Manager.ifFinishLayout = false;
                Manager.ifUpFloorLayoutBegin = new List<bool>();
                Manager.ifUpFloorInitializeOk = new List<bool>();
                Manager.ifUpFloorBoundaryForceOK = new List<bool>();
                Manager.ifUpFloorBarrierForceOK = new List<bool>();
                Manager.ifUpFloorPushForceOK = new List<bool>();
                Manager.ifUpFloorPullForceOK = new List<bool>();
                Manager.boundaryCount = 0;
                Manager.barrierCount = 0;
                Manager.pushCount = 0;
                Manager.upFloorCount = 0;
                Manager.ifPushModeChange = false;
                Manager.restart = false;//【之后可能改位置，改到代理重新计算时】
                Manager.ifEvaluateOK = false;
                Manager.ifAvoidCalculateTwice = false;
                StaticObject.groundBoundaryIndex = new List<List<int>>();
                StaticObject.groundBoundaryIndex.Add(new List<int>());//补上首层
                SiteInfo.buildingDensityActual = 0;
                SiteInfo.groundFloorAreaActual = 0;

                for (int i = 0; i < StaticObject.buildingPerFloor.Count; i++)
                {
                    if ((i == 0) || (i == 1))
                    {
                        Manager.ifUpFloorLayoutBegin.Add(true);
                        if (i == 0)
                        {
                            Manager.ifUpFloorInitializeOk.Add(true);
                            Manager.ifUpFloorBoundaryForceOK.Add(true);
                            Manager.ifUpFloorBarrierForceOK.Add(true);
                            Manager.ifUpFloorPushForceOK.Add(true);
                            Manager.ifUpFloorPullForceOK.Add(true);
                        }
                        else
                        {
                            Manager.ifUpFloorInitializeOk.Add(false);
                            Manager.ifUpFloorBoundaryForceOK.Add(false);
                            Manager.ifUpFloorBarrierForceOK.Add(false);
                            Manager.ifUpFloorPushForceOK.Add(false);
                            Manager.ifUpFloorPullForceOK.Add(false);
                        }
                    }
                    else
                    {
                        Manager.ifUpFloorLayoutBegin.Add(false);
                        Manager.ifUpFloorInitializeOk.Add(false);
                        Manager.ifUpFloorBoundaryForceOK.Add(false);
                        Manager.ifUpFloorBarrierForceOK.Add(false);
                        Manager.ifUpFloorPushForceOK.Add(false);
                        Manager.ifUpFloorPullForceOK.Add(false);
                    }
                }

                //计算二层及以上底面扩展后面积
                for (int i = 0; i < StaticObject.floorBoundary.Count; i++)
                {
                    if (i == 0)//首层布置完了，不计算了
                    {
                        StaticObject.AvailableArea = new List<List<double>>();
                        StaticObject.AvailableArea.Add(null);
                    }
                    else//二层及以上，计算各边线对应的面积，用于根据面积选择初始位点
                    {
                        if (StaticObject.floorBoundary[i] != null)//该层边界不为空
                        {
                            List<double> tempArea = new List<double>();
                            for (int j = 0; j < StaticObject.floorBoundary[i].Length; j++)
                            {
                                if (StaticObject.floorBoundary[i][j] != null)
                                {
                                    AreaMassProperties compute = AreaMassProperties.Compute(StaticObject.floorBoundary[i][j]);
                                    tempArea.Add(compute.Area);
                                }
                            }
                            StaticObject.AvailableArea.Add(tempArea);
                        }
                        else//该层边界为空
                        {
                            StaticObject.AvailableArea.Add(null);
                        }
                    }
                }
                #endregion

                //上层数据搭建
                for (int i = 0; i < StaticObject.buildingPerFloor.Count; i++)//遍历所有待布置楼层
                {
                    if (i == 0)//首层已经布置过了，直接添加null
                    {
                        // 二层及以上数据初始化
                        AddData(1, -1);
                        //每层添加null占位
                        for (int j = 0; j < StaticObject.buildingPerFloor.Count; j++)
                        {
                            AddData(0, -1);
                        }
                    }
                    else//二层及以上楼层
                    {
                        //若该层存在待布置场馆
                        if (StaticObject.buildingPerFloor[i] != null)
                        {
                            //添加站位
                            AddData(2, i);
                            //遍历该层所有待布置场馆
                            for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                            {
                                //该场馆存在
                                if (StaticObject.buildingPerFloor[i][j] != null)
                                {
                                    //获取场馆类型
                                    StaticObject.buildingTypesUpFloor[i].Add(Tool.GetBuildingType(StaticObject.buildingPerFloor[i][j]));
                                    //更新StaticObject二层及以上数据
                                    GetShape(StaticObject.buildingPerFloor[i][j], StaticObject.buildingsUpFloor[i], StaticObject.baseBoundaryUpFloor[i], StaticObject.ceilingBoundaryUpFloor[i], StaticObject.courtUpFloor[i], StaticObject.auxiliaryUpFloor[i], StaticObject.baseCenterUpFloor[i], StaticObject.baseHalfXUpFloor[i], StaticObject.baseHalfYUpFloor[i]);
                                }
                            }
                        }
                    }
                }
            }
            #endregion
        }
        //调整首层大厅位置至用地中心附近
        public void AdjustGroundFloorLobby()
        {
            double detaX = (StaticObject.boundaryValue[1] - StaticObject.boundaryValue[0]) * 0.5;
            double detaY = (StaticObject.boundaryValue[3] - StaticObject.boundaryValue[2]) * 0.5;
            double ratioX = 0.25;
            double ratioY = 0.25;
            //用于随机增量
            Random random = new Random(Tool.random.Next());
            double moveX = random.NextDouble() * detaX * ratioX;
            double moveY = random.NextDouble() * detaY * ratioY;
            Point3d newLocation;
            if (Tool.GetBool())//X轴正
            {
                if (Tool.GetBool())//Y轴正
                {
                    newLocation = new Point3d(moveX, moveY, 0) + SiteInfo.siteRetreat.Boundingbox.Center;
                }
                else//Y轴负
                {
                    newLocation = new Point3d(moveX, -moveY, 0) + SiteInfo.siteRetreat.Boundingbox.Center;
                }
            }
            else//X轴负
            {
                if (Tool.GetBool())//Y轴正
                {
                    newLocation = new Point3d(-moveX, moveY, 0) + SiteInfo.siteRetreat.Boundingbox.Center;
                }
                else//Y轴负
                {
                    newLocation = new Point3d(-moveX, -moveY, 0) + SiteInfo.siteRetreat.Boundingbox.Center;
                }
            }
            StaticObject.buildingPerFloor[0][0].Trans(Transform.Translation(newLocation - StaticObject.baseCenter[0]));
            StaticObject.baseCenter[0] = newLocation;
        }
        //获取建筑重叠、或未满足建筑间距产生的斥力
        public Vector3d GetOverlapVector(ITrans first, int i, ITrans second, int j, double distance)
        {
            double ratio = 0.05;
            double detaX = Math.Abs(StaticObject.baseCenter[j].X - StaticObject.baseCenter[i].X);
            double distanceXMin = StaticObject.baseHalfX[i] + StaticObject.baseHalfX[j] + distance;
            double detaY = Math.Abs(StaticObject.baseCenter[j].Y - StaticObject.baseCenter[i].Y);
            double distanceYMin = StaticObject.baseHalfY[i] + StaticObject.baseHalfY[j] + distance;
            if ((detaX < distanceXMin) && (detaY < distanceYMin))//重叠时
            {
                Vector3d distanceBetween = new Vector3d(StaticObject.baseCenter[i].X - StaticObject.baseCenter[j].X, StaticObject.baseCenter[i].Y - StaticObject.baseCenter[j].Y, 0);//形心间的长度
                Vector3d move;
                //计算形心之间的推力
                if (distanceBetween.Length < 1)//距离过近
                {
                    move = distanceBetween * 0.5;
                }
                else//距离不过近
                {
                    move = distanceBetween * 0.5 * ratio;
                }
                return move;
            }
            else { return Vector3d.Zero; }
        }
        //获取场馆相对位置
        public void GetBuildingPosition(double distance, List<List<int>> position)
        {
            double tempDist = distance;
            for (int i = 0; i < StaticObject.floorCount; i++)
            {
                for (int j = 0; j < StaticObject.floorCount; j++)
                {
                    if (i != j)
                    {
                        distance = tempDist;
                        if ((i == 0) || (j == 0) || (StaticObject.buildingTypes[i] == BuildingType.办公) || (StaticObject.buildingTypes[j] == BuildingType.办公))
                        {
                            distance = 0;
                        }
                        double detaX = Math.Abs(StaticObject.baseCenter[j].X - StaticObject.baseCenter[i].X);
                        double distanceXMin = StaticObject.baseHalfX[i] + StaticObject.baseHalfX[j] + distance;
                        double detaY = Math.Abs(StaticObject.baseCenter[j].Y - StaticObject.baseCenter[i].Y);
                        double distanceYMin = StaticObject.baseHalfY[i] + StaticObject.baseHalfY[j] + distance;
                        //获取其他建筑相对位置
                        position[i].Add(GetOtherBuildingPosition(detaX, distanceXMin, detaY, distanceYMin, i, j));
                    }
                    else
                    {
                        position[i].Add(-1);
                    }
                }
            }
        }
        //场馆距离过远时相互吸引
        public Vector3d GetPullVector(ITrans first, int i, ITrans second, int j, double distance, List<int> position, List<int> adjacent)
        {
            double ratio = 0.1;
            double detaX = Math.Abs(StaticObject.baseCenter[j].X - StaticObject.baseCenter[i].X);
            double distanceXMin = StaticObject.baseHalfX[i] + StaticObject.baseHalfX[j] + distance;
            double detaY = Math.Abs(StaticObject.baseCenter[j].Y - StaticObject.baseCenter[i].Y);
            double distanceYMin = StaticObject.baseHalfY[i] + StaticObject.baseHalfY[j] + distance;
            if (((detaX >= distanceXMin) || (detaY >= distanceYMin)) && (!((detaX < distanceXMin) && (detaY < distanceYMin))))//远离时
            {
                double moveX = detaX - distanceXMin;
                double moveY = detaY - distanceYMin;
                Vector3d move = Vector3d.Zero;
                //i在j右上区域
                if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (Math.Abs(moveX) > Math.Abs(moveY))//向Y方向移动
                    {
                        Vector3d temp = new Vector3d(-moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    else
                    {
                        Vector3d temp = new Vector3d(0, -moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    adjacent.Add(-1);//场馆毗邻关系
                }
                //i在j正右上区域
                else if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y >= StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (moveX != 0)
                    {
                        Vector3d temp = new Vector3d(-moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaX - distanceXMin) < 0.0001)//场馆毗邻关系
                    { adjacent.Add(0); }
                    else { adjacent.Add(-1); }
                }
                //i在j正右下区域
                else if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (moveX != 0)
                    {
                        Vector3d temp = new Vector3d(-moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaX - distanceXMin) < 0.0001)
                    {
                        adjacent.Add(0);
                    }
                    else { adjacent.Add(-1); }
                }
                //i在j右下区域
                else if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (Math.Abs(moveX) > Math.Abs(moveY))
                    {
                        Vector3d temp = new Vector3d(-moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    else
                    {
                        Vector3d temp = new Vector3d(0, moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    adjacent.Add(-1);//场馆毗邻关系
                }
                //i在j正下右区域
                else if ((StaticObject.baseCenter[i].X >= StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                {
                    if (moveY != 0)
                    {
                        Vector3d temp = new Vector3d(0, moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaY - distanceYMin) < 0.0001)
                    {
                        adjacent.Add(1);
                    }
                    else { adjacent.Add(-1); }
                }
                //i在j正下左区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                {
                    if (moveY != 0)
                    {
                        Vector3d temp = new Vector3d(0, moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaY - distanceYMin) < 0.0001)
                    {
                        adjacent.Add(1);
                    }
                    else { adjacent.Add(-1); }
                }
                //i在j左下区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (Math.Abs(moveX) > Math.Abs(moveY))
                    {
                        Vector3d temp = new Vector3d(moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    else
                    {
                        Vector3d temp = new Vector3d(0, moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    adjacent.Add(-1);
                }
                //i在j正左下区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y <= StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (moveX != 0)
                    {
                        Vector3d temp = new Vector3d(moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaX - distanceXMin) < 0.0001)
                    {
                        adjacent.Add(2);
                    }
                    else { adjacent.Add(-1); }
                }
                //i在j正左上区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (moveX != 0)
                    {
                        Vector3d temp = new Vector3d(moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaX - distanceXMin) < 0.0001)
                    {
                        adjacent.Add(2);
                    }
                    else { adjacent.Add(-1); }
                }
                //i在j左上区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (Math.Abs(moveX) > Math.Abs(moveY))
                    {
                        Vector3d temp = new Vector3d(moveX, 0, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    else
                    {
                        Vector3d temp = new Vector3d(0, -moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    adjacent.Add(-1);
                }
                //i在j正上左区域
                else if ((StaticObject.baseCenter[i].X <= StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                {
                    if (moveY != 0)
                    {
                        Vector3d temp = new Vector3d(0, -moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaY - distanceYMin) < 0.0001)
                    {
                        adjacent.Add(3);
                    }
                    else { adjacent.Add(-1); }
                }
                //i在j正上右区域
                else
                {
                    if (moveY != 0)
                    {
                        Vector3d temp = new Vector3d(0, -moveY, 0);
                        if (temp.Length > 0.5)
                        { move = temp * ratio; }
                        else
                        { move = temp * 0.5; }
                    }
                    if (Math.Abs(detaY - distanceYMin) < 0.0001)
                    {
                        adjacent.Add(3);
                    }
                    else { adjacent.Add(-1); }
                }
                return move;
            }
            else
            {
                adjacent.Add(-1);
                return Vector3d.Zero;
            }
        }
        //吸引向量筛选
        public Vector3d FilterPullVector(List<Vector3d> vectorPull, List<int> adjacent, List<int> position)
        {
            Vector3d temp = Vector3d.Zero;
            bool hasFirst = false;
            for (int i = 0; i < vectorPull.Count; i++)
            {
                if ((vectorPull[i].Length != 0) && (!hasFirst))
                {
                    temp = vectorPull[i];
                    hasFirst = true;
                }
                if ((vectorPull[i].Length <= temp.Length) && (vectorPull[i].Length != 0))//比temp短且无相邻遮挡就可以赋值
                {
                    //右无贴临
                    if ((vectorPull[i].X > 0) && (!GetAdjacentBuilding(2, adjacent)))
                    {
                        temp = vectorPull[i];
                    }
                    //下无贴临
                    else if ((vectorPull[i].Y < 0) && (!GetAdjacentBuilding(3, adjacent)))
                    {
                        temp = vectorPull[i];
                    }
                    //左无贴临
                    else if ((vectorPull[i].X < 0) && (!GetAdjacentBuilding(0, adjacent)))
                    {
                        temp = vectorPull[i];
                    }
                    //上无贴临
                    else if ((vectorPull[i].Y > 0) && (!GetAdjacentBuilding(1, adjacent)))
                    {
                        temp = vectorPull[i];
                    }
                    else
                    { temp = Vector3d.Zero; }
                }
            }
            return temp;
        }
        //不断寻找可用的拉力向量，若找不到则返回零向量
        //获取其他建筑相对位置关系:【上偏右】11；【右偏上】4；【右】0；【右偏下】5；【下偏右】6；【下】1；【下偏左】7；【左偏下】8；【左】2；【左偏上】9；【上偏左】10；【上】3
        public int GetOtherBuildingPosition(double detaX, double distanceXMin, double detaY, double distanceYMin, int i, int j)
        {
            double precise = 0.001;//【此处经常出问题】允许的误差0.00000000000001
            double testX = detaX - distanceXMin;//误差检测
            double testY = detaY - distanceYMin;//误差检测

            #region 远离时
            if (((detaX - distanceXMin > -precise) || (detaY - distanceYMin > -precise)) && ((detaX - distanceXMin > -precise) && (detaY - distanceYMin > -precise)))
            {
                double moveX = Math.Abs(detaX - distanceXMin);
                double moveY = Math.Abs(detaY - distanceYMin);
                //i在j右上区域
                if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 4; }
                    else { return 11; }
                }
                //i在j正右上区域
                else if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y >= StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 0; }
                //i在j正右下区域
                else if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 0; }
                //i在j右下区域
                else if ((StaticObject.baseCenter[i].X > StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 5; }
                    else { return 6; }
                }
                //i在j正下右区域
                else if ((StaticObject.baseCenter[i].X >= StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                { return 1; }
                //i在j正下左区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                { return 1; }
                //i在j左下区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y < StaticObject.baseCenter[j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 8; }
                    else { return 7; }
                }
                //i在j正左下区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y <= StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 2; }
                //i在j正左上区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 2; }
                //i在j左上区域
                else if ((StaticObject.baseCenter[i].X < StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (moveX > moveY) { return 9; }
                    else { return 10; }
                }
                //i在j正上左区域
                else if ((StaticObject.baseCenter[i].X <= StaticObject.baseCenter[j].X) && (StaticObject.baseCenter[i].Y > StaticObject.baseCenter[j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                { return 3; }
                //i在j正上右区域
                else
                { return 3; }
            }
            #endregion

            #region 不远离也不相交
            else if (!((detaX - distanceXMin <= -precise) && (detaY - distanceYMin <= -precise)))
            {
                //i在j右侧
                if ((StaticObject.baseCenter[j].X < StaticObject.baseCenter[i].X) && (detaX >= detaY))
                {
                    //i在j右上
                    if (StaticObject.baseCenter[j].Y < StaticObject.baseCenter[i].Y)
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 3; }
                        else//i纵向
                        { return 0; }
                    }
                    else//i在j右下
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 1; }
                        else
                        { return 0; }
                    }
                }
                //i在j下侧
                else if ((StaticObject.baseCenter[j].Y > StaticObject.baseCenter[i].Y) && (detaX < detaY))
                {
                    //i在j下右
                    if (StaticObject.baseCenter[j].X < StaticObject.baseCenter[i].X)
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 1; }
                        else
                        { return 0; }
                    }
                    else//i在j下左
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 1; }
                        else
                        { return 2; }
                    }
                }
                //i在j左侧
                else if ((StaticObject.baseCenter[j].X > StaticObject.baseCenter[i].X) && (detaX >= detaY))
                {
                    //i在j左下
                    if (StaticObject.baseCenter[j].Y > StaticObject.baseCenter[i].Y)
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 1; }
                        else
                        { return 2; }
                    }
                    else//i在j左上
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 3; }
                        else
                        { return 2; }
                    }
                }
                //i在j上侧
                else
                {
                    //i在j上左
                    if (StaticObject.baseCenter[j].X > StaticObject.baseCenter[i].X)
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 3; }
                        else
                        { return 2; }
                    }
                    else//i在j上右
                    {
                        //i横向
                        if (Math.Round(detaX - distanceXMin, 2) < 0)
                        { return 3; }
                        else
                        { return 0; }
                    }
                }
            }
            #endregion

            #region 相交
            else
            {
                double ratioPlus = 1.2;//当建筑X向长到此系数时算在另一方向障碍物
                                       //i在j右侧
                if ((StaticObject.baseCenter[j].X < StaticObject.baseCenter[i].X) && (detaX >= detaY))
                {
                    //i在j右上
                    if (StaticObject.baseCenter[j].Y < StaticObject.baseCenter[i].Y)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfX[i] > detaX * ratioPlus)
                        { return 3; }
                        else
                        { return 0; }
                    }
                    else//i在j右下
                    {
                        //i水平向长
                        if (StaticObject.baseHalfX[i] > detaX * ratioPlus)
                        { return 1; }
                        else
                        { return 0; }
                    }
                }
                //i在j下侧
                else if ((StaticObject.baseCenter[j].Y > StaticObject.baseCenter[i].Y) && (detaX < detaY))
                {
                    //i在j下右
                    if (StaticObject.baseCenter[j].X < StaticObject.baseCenter[i].X)
                    {
                        //i垂直向长
                        if (StaticObject.baseHalfY[i] > detaY * ratioPlus)
                        { return 0; }
                        else
                        { return 1; }
                    }
                    else//i在j下左
                    {
                        //i垂直向长
                        if (StaticObject.baseHalfY[i] > detaY * ratioPlus)
                        { return 2; }
                        else
                        { return 1; }
                    }
                }
                //i在j左侧
                else if ((StaticObject.baseCenter[j].X > StaticObject.baseCenter[i].X) && (detaX >= detaY))
                {
                    //i在j左下
                    if (StaticObject.baseCenter[j].Y > StaticObject.baseCenter[i].Y)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfX[i] > detaX * ratioPlus)
                        { return 1; }
                        else
                        { return 2; }
                    }
                    else//i在j左上
                    {
                        //i水平向长
                        if (StaticObject.baseHalfX[i] > detaX * ratioPlus)
                        { return 3; }
                        else
                        { return 2; }
                    }
                }
                //i在j上侧
                else
                {
                    //i在j上左
                    if (StaticObject.baseCenter[j].X > StaticObject.baseCenter[i].X)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfY[i] > detaY * ratioPlus)
                        { return 2; }
                        else
                        { return 3; }
                    }
                    else//i在j上右
                    {
                        //i水平向长
                        if (StaticObject.baseHalfY[i] > detaY * ratioPlus)
                        { return 0; }
                        else
                        { return 3; }
                    }
                }
                #endregion
            }
        }
        //获取某方向与当前对象毗邻的建筑
        public bool GetAdjacentBuilding(int oritation, List<int> adjacent)
        {
            bool near = false;
            for (int i = 0; i < adjacent.Count; i++)
            {
                if (adjacent[i] == oritation)
                {
                    near = true;
                }
            }
            return near;
        }
        //获取某方向与当前对象毗邻的建筑
        public List<int> GetAdjacentBuilding(List<int> adjacent)
        {
            List<int> adjacentIndex = new List<int>();
            for (int i = 0; i < adjacent.Count; i++)
            {
                if (adjacent[i] != -1)
                {
                    adjacentIndex.Add(i);
                }
            }
            return adjacentIndex;
        }
        //更新建筑单体中心点
        public void UpdateCenterPoint(List<Vector3d> vectorTotal)
        {
            for (int i = 0; i < StaticObject.floorCount; i++)
            {
                double x = StaticObject.baseCenter[i].X + vectorTotal[i].X;
                double y = StaticObject.baseCenter[i].Y + vectorTotal[i].Y;
                double z = StaticObject.baseCenter[i].Z + vectorTotal[i].Z;
                StaticObject.baseCenter[i] = new Point3d(x, y, z);
            }
        }
        //移动是否超出边界
        public Vector3d IfMoveOverBoundary(Curve boundary, Curve test, Vector3d move)
        {
            Curve newTest = test.DuplicateCurve();
            newTest.Transform(Transform.Translation(move));//移动后的曲线位置
            CurveIntersections intersect = Intersection.CurveCurve(boundary, newTest, 0.1, 0.1);//是否出边
            List<Vector3d> testMove = new List<Vector3d>();//记录出边后各点的位移
            List<Point3d> vertices = GetVertices(test);//求待测曲线4个顶点
            List<Point3d> closestPt = new List<Point3d>();//出边后对应到边界上的各最近点
            int outsideCount = 0;//出界点

            Plane plane = new Plane();//边界所在平面
            boundary.TryGetPlane(out plane);//获取边界平面

            if (intersect.Count > 1)//如果交点≥2
            {
                for (int i = 0; i < 4; i++)//检测是否有点出界
                {
                    PointContainment ptContain = boundary.Contains(vertices[i], plane, 0);
                    if (ptContain == PointContainment.Outside)
                    {
                        outsideCount++;
                        double t = 0;
                        boundary.ClosestPoint(vertices[i], out t, 0);
                        closestPt.Add(boundary.PointAt(t));//边界上的最近点
                        Vector3d moveToBoundary = closestPt[i] - vertices[i];//到最近点的向量
                        testMove.Add(moveToBoundary);
                    }
                    else { closestPt.Add(vertices[i]); }//占位，为了计算到最近点索引不出错
                }
                if (testMove.Count == 0)//若没出界
                {
                    return Vector3d.Zero;
                }
                else//若出界了
                {
                    return GetLongestVector(testMove);//返回模数最长的向量
                }
            }
            else { return Vector3d.Zero; }
        }
        //收缩运动时移动是否超出边界
        public Vector3d IfShrinkMoveOverBoundary(Curve boundary, Curve test, Vector3d move, int item, double tryX, double tryY)
        {
            Curve newTest = test.DuplicateCurve();
            //不动时是否出边
            CurveIntersections intersectFirst = Intersection.CurveCurve(boundary, newTest, 0.1, 0.1);
            bool ifOverBoundaryForStart = false;
            if (intersectFirst.Count > 1)
            {
                ifOverBoundaryForStart = true;
            }
            //移动后是否出边
            newTest.Transform(Transform.Translation(move));//移动后的曲线位置
            CurveIntersections intersect = Intersection.CurveCurve(boundary, newTest, 0.1, 0.1);//是否出边
            List<Vector3d> testMove = new List<Vector3d>();//记录出边后各点的位移

            if (intersect.Count > 1)//如果交点≥2
            {
                for (int i = 0; i < intersect.Count; i++)//检测是否有点出界
                {
                    if ((move.X == 0) && (move.Y > 0))//向上移动造成出界
                    {
                        //求到交点的Y值
                        double tempY = intersect[i].PointA.Y - StaticObject.baseCenter[item].Y - StaticObject.baseHalfY[item];
                        if (Math.Round(tempY, 1) != 0)//如果能向Y轴移动
                        {
                            testMove.Add(new Vector3d(0, tempY, 0));
                        }
                        else//卡边移不了【可能出界】
                        {
                            if (tryX != 0)//X轴有移动的余地
                            {
                                if (Math.Abs(tryX) >= 1)//X轴移动量＞=1
                                {
                                    testMove.Add(new Vector3d(tryX * (Math.Abs(1 / tryX)), 1, 0));
                                }
                                else//X轴移动量<1
                                {
                                    testMove.Add(new Vector3d(tryX, Math.Abs(tryX), 0));
                                }
                            }
                            else
                            {
                                testMove.Add(Vector3d.Zero);
                            }
                        }
                    }
                    else if ((move.X == 0) && (move.Y < 0))//向下移动造成出界
                    {
                        //求到交点的Y值
                        double tempY = intersect[i].PointA.Y - StaticObject.baseCenter[item].Y + StaticObject.baseHalfY[item];
                        if (Math.Round(tempY, 1) != 0)//如果能向Y轴移动
                        {
                            testMove.Add(new Vector3d(0, tempY, 0));
                        }
                        else//卡边移不了【可能出界】
                        {
                            if (tryX != 0)//X轴有移动的余地
                            {
                                if (Math.Abs(tryX) >= 1)//X轴移动量＞=1
                                {
                                    testMove.Add(new Vector3d(tryX * (Math.Abs(1 / tryX)), 1, 0));
                                }
                                else//X轴移动量<1
                                {
                                    testMove.Add(new Vector3d(tryX, Math.Abs(tryX), 0));
                                }
                            }
                            else
                            {
                                testMove.Add(Vector3d.Zero);
                            }
                        }
                    }
                    else if ((move.X > 0) && (move.Y == 0))//向右移动造成出界
                    {
                        //求到交点的X值
                        double tempX = intersect[i].PointA.X - StaticObject.baseCenter[item].X - StaticObject.baseHalfX[item];
                        if (Math.Round(tempX, 1) != 0)//如果能向X轴移动
                        {
                            testMove.Add(new Vector3d(tempX, 0, 0));
                        }
                        else//卡边移不了【可能出界】
                        {
                            if (tryY != 0)//Y轴有移动的余地
                            {
                                if (Math.Abs(tryY) >= 1)//Y轴移动量＞=1
                                {
                                    testMove.Add(new Vector3d(1, tryY * (Math.Abs(1 / tryY)), 0));
                                }
                                else//Y轴移动量<1
                                {
                                    testMove.Add(new Vector3d(Math.Abs(tryY), tryY, 0));
                                }
                            }
                            else
                            {
                                testMove.Add(Vector3d.Zero);
                            }
                        }
                    }
                    else if ((move.X < 0) && (move.Y == 0))//向左移动造成出界
                    {
                        //求到交点的X值
                        double tempX = intersect[i].PointA.X - StaticObject.baseCenter[item].X + StaticObject.baseHalfX[item];
                        if (Math.Round(tempX, 1) != 0)//如果能向X轴移动
                        {
                            testMove.Add(new Vector3d(tempX, 0, 0));
                        }
                        else//卡边移不了【可能出界】
                        {
                            if (tryY != 0)//Y轴有移动的余地
                            {
                                if (Math.Abs(tryY) >= 1)//Y轴移动量＞=1
                                {
                                    testMove.Add(new Vector3d(1, tryY * (Math.Abs(1 / tryY)), 0));
                                }
                                else//Y轴移动量<1
                                {
                                    testMove.Add(new Vector3d(Math.Abs(tryY), tryY, 0));
                                }
                            }
                            else
                            {
                                testMove.Add(Vector3d.Zero);
                            }
                        }
                    }
                    else if ((move.X == 0) && (move.Y == 0))//无需移动
                    {
                        testMove.Add(move);
                    }
                    else//X、Y轴上
                    {
                        Vector3d x = IfShrinkMoveOverBoundary(boundary, test, new Vector3d(move.X, 0, 0), item, tryX, tryY);
                        Vector3d y = IfShrinkMoveOverBoundary(boundary, test, new Vector3d(0, move.Y, 0), item, tryX, tryY);
                        if ((x.Y != 0) && (y.X != 0))//X/Y轴单方向都移动不了而尝试向被选方向移动
                        {
                            testMove.Add(new Vector3d(tryX * (Math.Abs(1 / tryX)), tryY * (Math.Abs(1 / tryY)), 0)); //【不严谨的写法，搞不好会出界】
                        }
                        else
                        {
                            if (Math.Abs(x.X) - Math.Abs(y.Y) >= 0)//向X/Y轴绝对值大的一方移动
                            {
                                testMove.Add(new Vector3d(x.X, 0, 0));
                            }
                            else
                            {
                                testMove.Add(new Vector3d(0, y.Y, 0));
                            }

                        }
                    }
                }
                if (testMove.Count == 0)//若没出界
                {
                    return move;
                }
                else//若出界了
                {
                    return GetShortestVector(testMove);//返回模数最长的向量
                }
            }
            else { return move; }
        }
        //获取模数最长的向量
        public Vector3d GetLongestVector(List<Vector3d> testMove)
        {
            Vector3d temp = Vector3d.Zero;
            foreach (Vector3d v in testMove)
            {
                if (v.Length > temp.Length) { temp = v; }
            }
            return temp;
        }
        //获取模数短的向量
        public Vector3d GetShortestVector(List<Vector3d> testMove)
        {
            Vector3d temp = testMove[0];
            foreach (Vector3d v in testMove)
            {
                if (Math.Abs(v.Length) < Math.Abs(temp.Length)) { temp = v; }
            }
            return temp;
        }
        //获取曲线的4个顶点，用于认不出顶点的矩形
        public List<Point3d> GetVertices(Curve test)
        {
            List<Point3d> vertices = new List<Point3d>();
            BoundingBox testBox = test.GetBoundingBox(true);
            vertices.Add(testBox.Min);
            vertices.Add(testBox.Max);
            vertices.Add(new Point3d(testBox.Min.X, testBox.Max.Y, 0));
            vertices.Add(new Point3d(testBox.Max.X, testBox.Min.Y, 0));
            return vertices;
        }
        //判断建筑单体哪侧其他单体最少。orientation=0:X轴，1:Y轴，return:0：轴线正向，1：轴线负向
        public bool GetFewerBuilding(List<int> position, int i, int orientation)
        {
            int sum0 = 0;
            int sum1 = 0;
            for (int j = 0; j < position.Count; j++)
            {
                //统计X轴
                if (orientation == 0)
                {
                    if (position[i] == 2)//i在j左侧
                    { sum0 += 1; }
                    else//i在j右侧
                    { sum1 += 1; }
                }
                //统计Y轴
                else
                {
                    if (position[i] == 1)//i在j下侧
                    { sum0 += 1; }
                    else//i在j上侧
                    { sum1 += 1; }
                }
            }
            if (sum0 <= sum1) return true;
            else return false;
        }
        //判断是否有阻碍吸引到大厅的障碍物
        public bool IfHasBarrierToLobby(List<int> position, int lobbyPosition, int item, List<Vector3d> vectorPull)
        {
            //求到大厅的力
            Vector3d vectorToLobby = vectorPull[0];
            //求大厅方向的所有力列表
            //如果大厅相对item是正方向（不在斜角上）
            if ((lobbyPosition == 0) || (lobbyPosition == 1) || (lobbyPosition == 2) || (lobbyPosition == 3))
            {
                for (int i = 1; i < StaticObject.floorCount; i++)
                {
                    if (position[i] == lobbyPosition)//如果找到了障碍物就返回
                    {
                        if (vectorPull[i].Length < vectorToLobby.Length)//查看是否有距离小于到大厅的
                        {
                            return true;//有障碍
                        }
                    }
                }
                return false;
            }
            else//如果在大厅相对item在斜角上
            {
                //i在大厅的右上方，向下运动
                if (((lobbyPosition == 11) || (lobbyPosition == 4)) && (vectorPull[0].X == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 3)//i下方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                //i在大厅的右上方，向左运动
                else if (((lobbyPosition == 11) || (lobbyPosition == 4)) && (vectorPull[0].Y == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 0)//i下方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                //i在大厅的右下方，向上运动
                else if (((lobbyPosition == 5) || (lobbyPosition == 6)) && (vectorPull[0].X == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 1)//i下方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                //i在大厅的右上方，向左运动
                else if (((lobbyPosition == 5) || (lobbyPosition == 6)) && (vectorPull[0].Y == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 0)//i下方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                //i在大厅的右下方，向上运动
                else if (((lobbyPosition == 7) || (lobbyPosition == 8)) && (vectorPull[0].X == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 1)//i上方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                //i在大厅的右上方，向左运动
                else if (((lobbyPosition == 7) || (lobbyPosition == 8)) && (vectorPull[0].Y == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 2)//i右方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                //i在大厅的右下方，向下运动
                else if (((lobbyPosition == 9) || (lobbyPosition == 10)) && (vectorPull[0].X == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 3)//i下方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                //i在大厅的右上方，向右运动
                else if (((lobbyPosition == 9) || (lobbyPosition == 10)) && (vectorPull[0].Y == 0))
                {
                    for (int i = 1; i < StaticObject.floorCount; i++)
                    {
                        if (position[i] == 2)//i右方有障碍物
                        {
                            return true;//有障碍
                        }
                    }
                    return false;
                }
                else { return false; }
            }
        }
        //获取某个方向（正向）距离最近的建筑
        public int GetNearestBuildingInSpecificOrientation(int item, int orientation, List<int> position)
        {
            List<double> distance = new List<double>();
            //获取某方向间距
            for (int i = 0; i < position.Count; i++)
            {
                if (position[i] == orientation)//是所需方向
                {
                    int axis;//存储间距的轴向
                             //求解间距的轴向
                    if ((orientation == 0) || (orientation == 2))//左右
                    { axis = 0; }
                    else//上下
                    { axis = 1; }
                    double dis = GetDistanceBetweenTwoBuilding(item, i, orientation, axis);
                    distance.Add(dis);
                }
                else//不是所需方向
                {
                    distance.Add(-1);
                }
            }
            //筛选最小间距
            double minDistance = 1000000;
            for (int i = 0; i < distance.Count; i++)
            {
                if ((minDistance > distance[i]) && (distance[i] >= -0.001))
                {
                    minDistance = distance[i];
                }
            }
            return distance.IndexOf(minDistance);
        }
        //【不相交时】【此算法存在逻辑瑕疵】获取2个场馆间垂直/水平距离(减去最小间距限值)，当在正方向上时，给一个间距，当远离时，给最小的
        public double GetDistanceBetweenTwoBuilding(int from, int to, int orientation, int axis)//axis：X轴为0，Y轴为1
        {
            double distance;
            if (((orientation == 0) || (orientation == 2)) && (axis == 0))//求from左/右侧对象的X轴间距
            {
                distance = Math.Abs(StaticObject.baseCenter[to].X - StaticObject.baseCenter[from].X) - StaticObject.baseHalfX[to] - StaticObject.baseHalfX[from] - GetMinDistanceBetweenTwoBuilding(from, to);
                return distance;
            }
            else if (((orientation == 0) || (orientation == 2)) && (axis == 1))//求from左/右侧对象的Y轴间距
            {
                distance = Math.Abs(StaticObject.baseCenter[to].Y - StaticObject.baseCenter[from].Y);
                return distance;
            }
            else if (((orientation == 1) || (orientation == 3)) && (axis == 0))//求from上/下侧对象的X轴间距
            {
                distance = Math.Abs(StaticObject.baseCenter[to].X - StaticObject.baseCenter[from].X);
                return distance;
            }
            else if (((orientation == 1) || (orientation == 3)) && (axis == 1))//求from上/下侧对象的Y轴间距
            {
                distance = Math.Abs(StaticObject.baseCenter[to].Y - StaticObject.baseCenter[from].Y) - StaticObject.baseHalfY[to] - StaticObject.baseHalfY[from] - GetMinDistanceBetweenTwoBuilding(from, to);
                return distance;
            }
            else //不正对着时
            {
                if (axis == 0)//X轴间距
                {
                    distance = Math.Abs(StaticObject.baseCenter[to].X - StaticObject.baseCenter[from].X) - StaticObject.baseHalfX[to] - StaticObject.baseHalfX[from] - GetMinDistanceBetweenTwoBuilding(from, to);
                    return distance;
                }
                else//Y轴间距
                {
                    distance = Math.Abs(StaticObject.baseCenter[to].Y - StaticObject.baseCenter[from].Y) - StaticObject.baseHalfY[to] - StaticObject.baseHalfY[from] - GetMinDistanceBetweenTwoBuilding(from, to);
                    return distance;
                }
            }
        }
        //获取两个建筑间的必要间距
        public double GetMinDistanceBetweenTwoBuilding(int first, int second)
        {
            if ((StaticObject.buildingTypes[first] == BuildingType.大厅) || (StaticObject.buildingTypes[second] == BuildingType.大厅) || (StaticObject.buildingTypes[first] == BuildingType.办公) || (StaticObject.buildingTypes[second] == BuildingType.办公))
            {
                return 0;
            }
            else { return StaticObject.baseOffset; }
        }
        //【！！！优先检查BUG】获取布局收缩时可移动的向量(向大厅中心靠拢)
        public Vector3d GetShrinkVector(int i, List<int> position)
        {
            double deltaX = StaticObject.baseCenter[0].X - StaticObject.baseCenter[i].X;//对象中心到大厅中心，X轴
            double deltaY = StaticObject.baseCenter[0].Y - StaticObject.baseCenter[i].Y;//对象中心到大厅中心，Y轴
            double minX = StaticObject.baseHalfX[0] + StaticObject.baseHalfX[i];//对象到大厅X轴最小距离
            double minY = StaticObject.baseHalfY[0] + StaticObject.baseHalfY[i];//对象到大厅Y轴最小距离
            int nearestX = 0;//对象移动X轴最近干扰量
            int nearestY = 0;//对象移动Y轴最近干扰量
            double tryX = 0;//备选X方向可移动量
            double tryY = 0;//备选Y方向可移动量
            double finalX = 0;//对象实际移动量，X轴
            double finalY = 0;//对象实际移动量，Y轴
            double temp = 1;//当指定方向上没有遮挡物时，距离占位，用于计算备选移动方向的可能性
            #region 对象在大厅右
            if (position[0] == 0)
            {
                //对象远离大厅
                if ((Math.Abs(deltaX) - minX) > 0.001)
                {
                    nearestX = GetNearestBuildingInSpecificOrientation(i, 0, position);//获取对象左侧最近的场馆
                    if ((nearestX != 0) && (nearestX != -1))//最近的不是大厅
                    {
                        double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X;//求对象到左侧最近场馆中心距离
                        finalX = -(Math.Abs(tempX) - (StaticObject.baseHalfX[nearestX] + StaticObject.baseHalfX[i] + StaticObject.baseOffset));//求对象X轴实际移动向量
                        tryX = finalX;
                    }
                    else//最近的是大厅
                    {
                        finalX = -(Math.Abs(deltaX) - minX);//求对象X轴实际移动向量
                        tryX = finalX;
                    }
                    if ((Math.Round(finalX, 2) == 0) || (Manager.shrinkCount > 2))//若X轴不懂或被边界卡住了
                    {
                        if (deltaY > 0)//对象有向上移动趋势
                        {
                            nearestY = GetNearestBuildingInSpecificOrientation(i, 1, position);//获取对象上侧最近的场馆
                            if (nearestY != -1)//最近的存在
                            {
                                double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset;//求对象到左侧最近场馆中心距离
                                if (tempY > deltaY)//到大厅中心更近
                                {
                                    if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = deltaY;//移到大厅中心Y坐标
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = tempY;//移到nearest
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                            }
                            else//最近的是大厅，对象上部没有东西
                            {
                                if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                {
                                    finalY = deltaY;//移到大厅中心Y坐标
                                    tryY = temp;
                                    finalX = 0;
                                }
                                else//若Y移动距离小于X
                                {
                                    finalY = 0;
                                }
                            }
                        }
                        else//对象有向下移动趋势
                        {
                            nearestY = GetNearestBuildingInSpecificOrientation(i, 3, position);//获取对象下侧最近的场馆
                            if (nearestY != -1)//最近的存在
                            {
                                double tempY = -(Math.Abs(StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y) - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                                if (Math.Abs(tempY) > Math.Abs(deltaY))//到大厅中心更近
                                {
                                    if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = deltaY;//移到大厅中心Y坐标
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = tempY;//移到nearest
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                            }
                            else//最近的是大厅，对象下上部没有东西
                            {
                                if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                {
                                    finalY = deltaY;//移到大厅中心Y坐标
                                    tryY = -temp;
                                    finalX = 0;
                                }
                                else//若Y移动距离小于X
                                {
                                    finalY = 0;
                                }
                            }
                        }
                    }
                }
                //对象贴临大厅【与远离大厅时的Y算法相同】
                else
                {
                    if (deltaY > 0)//对象有向上移动趋势
                    {
                        nearestY = GetNearestBuildingInSpecificOrientation(i, 1, position);//获取对象上侧最近的场馆
                        if (nearestY != -1)//最近的存在
                        {
                            double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset;//求对象到左侧最近场馆中心距离
                            if (tempY > deltaY)//到大厅中心更近
                            {
                                finalY = deltaY;//移到大厅中心Y坐标
                                tryY = tempY;
                            }
                            else//到nearest更近
                            {
                                finalY = tempY;//移到nearest
                                tryY = tempY;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalY = deltaY;//移到大厅中心Y坐标
                            tryY = temp;
                        }
                    }
                    else//对象有向下移动趋势
                    {
                        nearestY = GetNearestBuildingInSpecificOrientation(i, 3, position);//获取对象下侧最近的场馆
                        if (nearestY != -1)//最近的存在
                        {
                            double tempY = -(Math.Abs(StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y) - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                            if (Math.Abs(tempY) > Math.Abs(deltaY))//到大厅中心更近
                            {
                                finalY = deltaY;//移到大厅中心Y坐标
                                tryY = tempY;
                            }
                            else//到nearest更近
                            {
                                finalY = tempY;//移到nearest
                                tryY = tempY;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalY = deltaY;//移到大厅中心Y坐标
                            tryY = -temp;
                        }
                    }
                }
            }
            #endregion

            #region 对象在大厅下
            else if (position[0] == 1)
            {
                //对象远离大厅
                if ((deltaY - minY) > 0.01)
                {
                    nearestY = GetNearestBuildingInSpecificOrientation(i, 1, position);//获取对象上侧最近的场馆
                    if ((nearestY != 0) && (nearestY != -1))//最近的不是大厅
                    {
                        double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y;//求对象到上侧最近场馆中心距离
                        finalY = tempY - (StaticObject.baseHalfY[nearestY] + StaticObject.baseHalfY[i] + StaticObject.baseOffset);//求对象Y轴实际移动向量
                        tryY = finalY;
                    }
                    else//最近的是大厅
                    {
                        finalY = deltaY - minY;//求对象Y轴实际移动向量
                        tryY = finalY;
                    }
                    if ((finalY == 0) || (Manager.shrinkCount > 2))//若X轴不懂或被边界卡住了
                    {
                        if (deltaX > 0)//对象有向右移动趋势
                        {
                            nearestX = GetNearestBuildingInSpecificOrientation(i, 2, position);//获取对象右侧最近的场馆
                            if (nearestX != -1)//最近的存在
                            {
                                double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset;//求对象到右侧最近场馆中心距离
                                if (tempX > deltaX)//到大厅中心更近
                                {
                                    if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = deltaX;//移到大厅中心X坐标
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = tempX;//移到nearest
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                            }
                            else//最近的是大厅
                            {
                                if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                {
                                    finalX = deltaX;//移到大厅中心X坐标
                                    tryX = temp;
                                    finalY = 0;
                                }
                                else
                                {
                                    finalX = 0;
                                }
                            }
                        }
                        else//对象有向左移动趋势
                        {
                            nearestX = GetNearestBuildingInSpecificOrientation(i, 0, position);//获取对象左侧最近的场馆
                            if (nearestX != -1)//最近的存在
                            {
                                double tempX = -(Math.Abs(StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X) - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                                if (Math.Abs(tempX) > Math.Abs(deltaX))//到大厅中心更近
                                {
                                    if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = deltaX;//移到大厅中心X坐标
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = tempX;//移到nearest
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                            }
                            else//最近的是大厅
                            {
                                if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                {
                                    finalX = deltaX;//移到大厅中心X坐标
                                    tryX = -temp;
                                    finalY = 0;
                                }
                                else
                                {
                                    finalX = 0;
                                }
                            }
                        }
                    }
                }
                //对象贴临大厅【与远离时X轴算法一直】
                else
                {
                    if (deltaX > 0)//对象有向右移动趋势
                    {
                        nearestX = GetNearestBuildingInSpecificOrientation(i, 2, position);//获取对象右侧最近的场馆
                        if (nearestX != -1)//最近的存在
                        {
                            double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset;//求对象到右侧最近场馆中心距离
                            if (tempX > deltaX)//到大厅中心更近
                            {
                                finalX = deltaX;//移到大厅中心X坐标
                                tryX = tempX;
                            }
                            else//到nearest更近
                            {
                                finalX = tempX;//移到nearest
                                tryX = tempX;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalX = deltaX;//移到大厅中心X坐标
                            tryX = temp;
                        }
                    }
                    else//对象有向左移动趋势
                    {
                        nearestX = GetNearestBuildingInSpecificOrientation(i, 0, position);//获取对象左侧最近的场馆
                        if (nearestX != -1)//最近的存在
                        {
                            double tempX = -(Math.Abs(StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X) - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                            if (Math.Abs(tempX) > Math.Abs(deltaX))//到大厅中心更近
                            {
                                finalX = deltaX;//移到大厅中心X坐标
                                tryX = tempX;
                            }
                            else//到nearest更近
                            {
                                finalX = tempX;//移到nearest
                                tryX = tempX;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalX = deltaX;//移到大厅中心X坐标
                            tryX = -temp;
                        }
                    }
                }
            }
            #endregion

            #region 对象在大厅左
            else if (position[0] == 2)
            {
                //对象远离大厅
                if ((deltaX - minX) > 0.001)
                {
                    nearestX = GetNearestBuildingInSpecificOrientation(i, 2, position);//获取对象右侧最近的场馆
                    if ((nearestX != 0) && (nearestX != -1))//最近的不是大厅
                    {
                        double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X;//求对象到右侧最近场馆中心距离
                        finalX = tempX - (StaticObject.baseHalfX[nearestX] + StaticObject.baseHalfX[i] + StaticObject.baseOffset);//求对象X轴实际移动向量
                        tryX = finalX;
                    }
                    else//最近的是大厅
                    {
                        finalX = deltaX - minX;//求对象X轴实际移动向量
                        tryX = finalX;
                    }
                    if ((finalX == 0) || (Manager.shrinkCount > 2))//若X轴不动或被边界卡住了
                    {
                        if (deltaY > 0)//对象有向上移动趋势
                        {
                            nearestY = GetNearestBuildingInSpecificOrientation(i, 1, position);//获取对象上侧最近的场馆
                            if (nearestY != -1)//最近的存在
                            {
                                double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset;//求对象到左侧最近场馆中心距离
                                if (tempY > deltaY)//到大厅中心更近
                                {
                                    if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = deltaY;//移到大厅中心Y坐标
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = tempY;//移到nearest
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                            }
                            else//最近的是大厅
                            {
                                if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                {
                                    finalY = deltaY;//移到大厅中心Y坐标
                                    tryY = temp;
                                    finalX = 0;
                                }
                                else//若Y移动距离小于X
                                {
                                    finalY = 0;
                                }
                            }
                        }
                        else//对象有向下移动趋势
                        {
                            nearestY = GetNearestBuildingInSpecificOrientation(i, 3, position);//获取对象下侧最近的场馆
                            if (nearestY != -1)//最近的存在
                            {
                                double tempY = -(Math.Abs(StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y) - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                                if (Math.Abs(tempY) > Math.Abs(deltaY))//到大厅中心更近
                                {
                                    if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = deltaY;//移到大厅中心Y坐标
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempY) > Math.Abs(finalX))//若Y移动距离大于X
                                    {
                                        finalY = tempY;//移到nearest
                                        tryY = tempY;
                                        finalX = 0;
                                    }
                                    else//若Y移动距离小于X
                                    {
                                        finalY = 0;
                                    }
                                }
                            }
                            else//最近的是大厅
                            {
                                if (Math.Abs(deltaY) > Math.Abs(finalX))//若Y移动距离大于X
                                {
                                    finalY = deltaY;//移到大厅中心Y坐标
                                    tryY = -temp;
                                    finalX = 0;
                                }
                                else//若Y移动距离小于X
                                {
                                    finalY = 0;
                                }
                            }
                        }
                    }
                }
                //对象贴临大厅【与远离大厅时的Y算法相同】
                else
                {
                    if (deltaY > 0)//对象有向上移动趋势
                    {
                        nearestY = GetNearestBuildingInSpecificOrientation(i, 1, position);//获取对象上侧最近的场馆
                        if (nearestY != -1)//最近的存在
                        {
                            double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset;//求对象到左侧最近场馆中心距离
                            if (tempY > deltaY)//到大厅中心更近
                            {
                                finalY = deltaY;//移到大厅中心Y坐标
                                tryY = tempY;
                            }
                            else//到nearest更近
                            {
                                finalY = tempY;//移到nearest
                                tryY = tempY;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalY = deltaY;//移到大厅中心Y坐标
                            tryY = temp;
                        }
                    }
                    else//对象有向下移动趋势
                    {
                        nearestY = GetNearestBuildingInSpecificOrientation(i, 3, position);//获取对象下侧最近的场馆
                        if (nearestY != -1)//最近的存在
                        {
                            double tempY = -(Math.Abs(StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y) - StaticObject.baseHalfY[nearestY] - StaticObject.baseHalfY[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                            if (Math.Abs(tempY) > Math.Abs(deltaY))//到大厅中心更近
                            {
                                finalY = deltaY;//移到大厅中心Y坐标
                                tryY = tempY;
                            }
                            else//到nearest更近
                            {
                                finalY = tempY;//移到nearest
                                tryY = tempY;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalY = deltaY;//移到大厅中心Y坐标
                            tryY = -temp;
                        }
                    }
                }
            }
            #endregion

            #region 对象在大厅上
            else if (position[0] == 3)
            {
                //对象远离大厅
                if ((Math.Abs(deltaY) - Math.Abs(minY)) > 0.01)
                {
                    nearestY = GetNearestBuildingInSpecificOrientation(i, 3, position);//获取对象下侧最近的场馆
                    if ((nearestY != 0) && (nearestY != -1))//最近的不是大厅
                    {
                        double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y;//求对象到上侧最近场馆中心距离
                        finalY = -(Math.Abs(tempY) - (StaticObject.baseHalfY[nearestY] + StaticObject.baseHalfY[i] + StaticObject.baseOffset));//求对象Y轴实际移动向量
                        tryY = finalY;
                    }
                    else//最近的是大厅
                    {
                        finalY = -(Math.Abs(deltaY) - minY);//求对象Y轴实际移动向量
                        tryY = finalY;
                    }
                    if ((finalY == 0) || (Manager.shrinkCount > 2))//若X轴不懂或被边界卡住了
                    {
                        if (deltaX > 0)//对象有向右移动趋势
                        {
                            nearestX = GetNearestBuildingInSpecificOrientation(i, 2, position);//获取对象右侧最近的场馆
                            if (nearestX != -1)//最近的存在
                            {
                                double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset;//求对象到右侧最近场馆中心距离
                                if (tempX > deltaX)//到大厅中心更近
                                {
                                    if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = deltaX;//移到大厅中心X坐标
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = tempX;//移到nearest
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                            }
                            else//最近的是大厅
                            {
                                if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                {
                                    finalX = deltaX;//移到大厅中心X坐标
                                    tryX = temp;
                                    finalY = 0;
                                }
                                else
                                {
                                    finalX = 0;
                                }
                            }
                        }
                        else//对象有向左移动趋势
                        {
                            nearestX = GetNearestBuildingInSpecificOrientation(i, 0, position);//获取对象左侧最近的场馆
                            if (nearestX != -1)//最近的存在
                            {
                                double tempX = -(Math.Abs(StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X) - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                                if (Math.Abs(tempX) > Math.Abs(deltaX))//到大厅中心更近
                                {
                                    if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = deltaX;//移到大厅中心X坐标
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                                else//到nearest更近
                                {
                                    if (Math.Abs(tempX) > Math.Abs(finalY))//若X移动距离大于Y
                                    {
                                        finalX = tempX;//移到nearest
                                        tryX = tempX;
                                        finalY = 0;
                                    }
                                    else
                                    {
                                        finalX = 0;
                                    }
                                }
                            }
                            else//最近的是大厅
                            {
                                if (Math.Abs(deltaX) > Math.Abs(finalY))//若X移动距离大于Y
                                {
                                    finalX = deltaX;//移到大厅中心X坐标
                                    tryX = -temp;
                                    finalY = 0;
                                }
                                else
                                {
                                    finalX = 0;
                                }
                            }
                        }
                    }
                }
                //对象贴临大厅【与远离时X轴算法一直】
                else
                {
                    if (deltaX > 0)//对象有向右移动趋势
                    {
                        nearestX = GetNearestBuildingInSpecificOrientation(i, 2, position);//获取对象右侧最近的场馆
                        if (nearestX != -1)//最近的存在
                        {
                            double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset;//求对象到右侧最近场馆中心距离
                            if (tempX > deltaX)//到大厅中心更近
                            {
                                finalX = deltaX;//移到大厅中心X坐标
                                tryX = tempX;
                            }
                            else//到nearest更近
                            {
                                finalX = tempX;//移到nearest
                                tryX = tempX;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalX = deltaX;//移到大厅中心X坐标
                            tryX = temp;
                        }
                    }
                    else//对象有向左移动趋势
                    {
                        nearestX = GetNearestBuildingInSpecificOrientation(i, 0, position);//获取对象左侧最近的场馆
                        if (nearestX != -1)//最近的存在
                        {
                            double tempX = -(Math.Abs(StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X) - StaticObject.baseHalfX[nearestX] - StaticObject.baseHalfX[i] - StaticObject.baseOffset);//求对象到左侧最近场馆中心距离
                            if (Math.Abs(tempX) > Math.Abs(deltaX))//到大厅中心更近
                            {
                                finalX = deltaX;//移到大厅中心X坐标
                                tryX = tempX;
                            }
                            else//到nearest更近
                            {
                                finalX = tempX;//移到nearest
                                tryX = tempX;
                            }
                        }
                        else//最近的是大厅
                        {
                            finalX = deltaX;//移到大厅中心X坐标
                            tryX = -temp;
                        }
                    }
                }
            }
            #endregion

            #region 对象在大厅右上角
            else if ((position[0] == 4) || (position[0] == 11))
            {
                //获取对象下侧最近的场馆
                nearestY = GetNearestBuildingInSpecificOrientation(i, 3, position);
                if (nearestY != -1)
                {
                    //求对象到上侧最近场馆中心距离
                    double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y;
                    //求对象Y轴实际移动向量
                    finalY = -(Math.Abs(tempY) - (StaticObject.baseHalfY[nearestY] + StaticObject.baseHalfY[i] + StaticObject.baseOffset));
                    tryY = finalY;
                    //如果到大厅更近，则到大厅中心Y坐标
                    if (Math.Abs(deltaY) < Math.Abs(finalY))
                    {
                        finalY = deltaY;
                    }
                }
                else
                {
                    finalY = deltaY;//求对象Y轴实际移动向量
                    tryY = -temp;
                }
                //获取对象左侧最近的场馆
                nearestX = GetNearestBuildingInSpecificOrientation(i, 0, position);
                if (nearestX != -1)
                {
                    //求对象到左侧最近场馆中心距离
                    double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X;
                    //求对象X轴实际移动向量
                    finalX = -(Math.Abs(tempX) - (StaticObject.baseHalfX[nearestX] + StaticObject.baseHalfX[i] + StaticObject.baseOffset));
                    tryX = finalX;
                    if (Math.Abs(finalY) <= Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        if (Math.Abs(deltaX) < Math.Abs(finalX))
                        {
                            finalX = deltaX;
                            finalY = 0;
                        }
                        else
                        {
                            finalY = 0;
                        }
                    }
                    else//Y可以移动，X轴变量要归零
                    {
                        finalX = 0;
                    }
                }
                else//指定方向没有对象
                {
                    tryX = -temp;
                    if (Math.Abs(finalY) < Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        finalX = deltaX;
                        finalY = 0;
                    }
                }
            }
            #endregion

            #region 对象在大厅右下角
            else if ((position[0] == 5) || (position[0] == 6))
            {
                //获取对象下侧最近的场馆
                nearestY = GetNearestBuildingInSpecificOrientation(i, 1, position);
                if (nearestY != -1)
                {
                    //求对象到上侧最近场馆中心距离
                    double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y;
                    //求对象Y轴实际移动向量
                    finalY = tempY - (StaticObject.baseHalfY[nearestY] + StaticObject.baseHalfY[i] + StaticObject.baseOffset);
                    tryY = finalY;
                    //如果到大厅更近，则到大厅中心Y坐标
                    if (Math.Abs(deltaY) < Math.Abs(finalY))
                    {
                        finalY = deltaY;
                    }
                }
                else
                {
                    finalY = deltaY;
                    tryY = temp;
                }
                //获取对象左侧最近的场馆
                nearestX = GetNearestBuildingInSpecificOrientation(i, 0, position);
                if (nearestX != -1)
                {
                    //求对象到左侧最近场馆中心距离
                    double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X;
                    //求对象X轴实际移动向量
                    finalX = -(Math.Abs(tempX) - (StaticObject.baseHalfX[nearestX] + StaticObject.baseHalfX[i] + StaticObject.baseOffset));
                    tryX = finalX;
                    if (Math.Abs(finalY) <= Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        if (Math.Abs(deltaX) < Math.Abs(finalX))
                        {
                            finalX = deltaX;
                            finalY = 0;
                        }
                        else
                        {
                            finalY = 0;
                        }
                    }
                    else//Y可以移动，X轴变量要归零
                    {
                        finalX = 0;
                    }
                }
                else//指定方向没有对象
                {
                    tryX = -temp;
                    if (Math.Abs(finalY) < Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        finalX = deltaX;
                        finalY = 0;
                    }
                }
            }
            #endregion

            #region 对象在大厅左下角
            else if ((position[0] == 7) || (position[0] == 8))
            {
                //获取对象下侧最近的场馆
                nearestY = GetNearestBuildingInSpecificOrientation(i, 1, position);
                if (nearestY != -1)
                {
                    //求对象到上侧最近场馆中心距离
                    double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y;
                    //求对象Y轴实际移动向量
                    finalY = tempY - (StaticObject.baseHalfY[nearestY] + StaticObject.baseHalfY[i] + StaticObject.baseOffset);
                    tryY = finalY;
                    //如果到大厅更近，则到大厅中心Y坐标
                    if (Math.Abs(deltaY) < Math.Abs(finalY))
                    {
                        finalY = deltaY;
                    }
                }
                else
                {
                    finalY = deltaY;
                    tryY = temp;
                }
                //获取对象右侧最近的场馆
                nearestX = GetNearestBuildingInSpecificOrientation(i, 2, position);
                if (nearestX != -1)
                {
                    //求对象到右侧最近场馆中心距离
                    double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X;
                    //求对象X轴实际移动向量
                    finalX = tempX - (StaticObject.baseHalfX[nearestX] + StaticObject.baseHalfX[i] + StaticObject.baseOffset);
                    tryX = finalX;
                    if (Math.Abs(finalY) <= Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        if (Math.Abs(deltaX) < Math.Abs(finalX))
                        {
                            finalX = deltaX;
                            finalY = 0;
                        }
                        else
                        {
                            finalY = 0;
                        }
                    }
                    else
                    {
                        finalX = 0;
                    }
                }
                else
                {
                    tryX = temp;
                    if (Math.Abs(finalY) < Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        finalX = deltaX;
                        finalY = 0;
                    }
                }
            }
            #endregion

            #region 对象在大厅左上角
            else if ((position[0] == 9) || (position[0] == 10))
            {
                //获取对象下侧最近的场馆
                nearestY = GetNearestBuildingInSpecificOrientation(i, 3, position);
                if (nearestY != -1)
                {
                    //求对象到上侧最近场馆中心距离
                    double tempY = StaticObject.baseCenter[nearestY].Y - StaticObject.baseCenter[i].Y;
                    //求对象Y轴实际移动向量
                    finalY = -(Math.Abs(tempY) - (StaticObject.baseHalfY[nearestY] + StaticObject.baseHalfY[i] + StaticObject.baseOffset));
                    tryY = finalY;
                    //如果到大厅更近，则到大厅中心Y坐标
                    if (Math.Abs(deltaY) < Math.Abs(finalY))
                    {
                        finalY = deltaY;
                    }
                }
                else
                {
                    finalY = deltaY;
                    tryY = -temp;
                }
                //获取对象右侧最近的场馆
                nearestX = GetNearestBuildingInSpecificOrientation(i, 2, position);
                if (nearestX != -1)//右侧有场馆
                {
                    //求对象到右侧最近场馆中心距离
                    double tempX = StaticObject.baseCenter[nearestX].X - StaticObject.baseCenter[i].X;
                    //求对象X轴实际移动向量
                    finalX = tempX - (StaticObject.baseHalfX[nearestX] + StaticObject.baseHalfX[i] + StaticObject.baseOffset);
                    tryX = finalX;
                    if (Math.Abs(finalY) <= Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        if (Math.Abs(deltaX) < Math.Abs(finalX))//如果到大厅更近
                        {
                            finalX = deltaX;
                            finalY = 0;
                        }
                        else
                        {
                            finalY = 0;
                        }
                    }
                    else
                    {
                        finalX = 0;
                    }
                }
                else//右侧无场馆
                {
                    tryX = temp;
                    if (Math.Abs(finalY) < Math.Abs(tryX))//Y轴移动没有X轴远，向X轴移
                    {
                        finalX = deltaX;
                        finalY = 0;
                    }
                }
            }
            #endregion

            //检查是否出界
            Vector3d move = IfShrinkMoveOverBoundary(SiteInfo.siteRetreat.Value, StaticObject.baseBoundary[i], new Vector3d(finalX, finalY, 0), i, tryX, tryY); ;
            return move;
        }
        //判断对象几面毗邻
        public double HowManyAdjacentBuilding(List<int> adjacent, out List<bool> isAdjacent)
        {
            bool up = false;//item在对象上
            bool down = false;//item在对象下
            bool left = false;//item在对象左
            bool right = false;//item在对象右
            for (int i = 0; i < adjacent.Count; i++)//判断各方向的毗邻情况
            {
                if (adjacent[i] == 3)//item在对象上
                {
                    up = true;
                }
                else if (adjacent[i] == 1)//item在对象下
                {
                    down = true;
                }
                else if (adjacent[i] == 2)//item在对象左
                {
                    left = true;
                }
                else if (adjacent[i] == 0)//item在对象右
                {
                    right = true;
                }
            }
            //构建毗邻结论列表
            isAdjacent = new List<bool>() { up, down, left, right };
            //计算毗邻方向总数
            int sum = 0;
            foreach (bool i in isAdjacent)
            {
                if (i)
                {
                    sum += 1;
                }
            }
            return sum;
        }
        //求合力
        public double SumVector(List<Vector3d> vectorTotal)
        {
            Vector3d sum = Vector3d.Zero;
            foreach (Vector3d vector in vectorTotal)
            {
                sum += vector;
            }
            return sum.Length;
        }
        //求有多少个true或者false
        public int HowManyBool(bool trueOrFalse, List<bool> items)
        {
            int count = 0;
            foreach (bool item in items)
            {
                if (trueOrFalse)
                {
                    if (item == true)
                    {
                        count++;
                    }
                }
                else
                {
                    if (item == false)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
        //批量添加null
        public void AddData(int type, int item)
        {
            if (type == 0)//添加null
            {
                StaticObject.buildingTypesUpFloor.Add(null);
                StaticObject.buildingsUpFloor.Add(null);
                StaticObject.auxiliaryUpFloor.Add(null);
                StaticObject.baseBoundaryUpFloor.Add(null);
                StaticObject.ceilingBoundaryUpFloor.Add(null);
                StaticObject.courtUpFloor.Add(null);
                StaticObject.baseCenterUpFloor.Add(null);
                StaticObject.baseHalfXUpFloor.Add(null);
                StaticObject.baseHalfYUpFloor.Add(null);
                StaticObject.barrier.Add(null);
            }
            else if (type == 1)//加各层列表
            {
                StaticObject.buildingTypesUpFloor = new List<List<BuildingType>>();
                StaticObject.buildingsUpFloor = new List<List<GH_Box>>();
                StaticObject.auxiliaryUpFloor = new List<List<GH_Box>>();
                StaticObject.baseBoundaryUpFloor = new List<List<Curve>>();
                StaticObject.ceilingBoundaryUpFloor = new List<List<Curve>>();
                StaticObject.courtUpFloor = new List<List<GH_Rectangle>>();
                StaticObject.baseCenterUpFloor = new List<List<Point3d>>();
                StaticObject.baseHalfXUpFloor = new List<List<double>>();
                StaticObject.baseHalfYUpFloor = new List<List<double>>();
                StaticObject.barrier = new List<List<Curve>>();
            }
            else if (type == 2)//加单层元素列表
            {
                StaticObject.buildingTypesUpFloor[item] = new List<BuildingType>();
                StaticObject.buildingsUpFloor[item] = new List<GH_Box>();
                StaticObject.auxiliaryUpFloor[item] = new List<GH_Box>();
                StaticObject.baseBoundaryUpFloor[item] = new List<Curve>();
                StaticObject.ceilingBoundaryUpFloor[item] = new List<Curve>();
                StaticObject.courtUpFloor[item] = new List<GH_Rectangle>();
                StaticObject.baseCenterUpFloor[item] = new List<Point3d>();
                StaticObject.baseHalfXUpFloor[item] = new List<double>();
                StaticObject.baseHalfYUpFloor[item] = new List<double>();
                StaticObject.barrier[item] = new List<Curve>();

            }
        }
        //获取场馆主要形体
        public void GetShape(ITrans toBeLayout, List<GH_Box> buildings, List<Curve> baseBoundary, List<Curve> ceilingBoundary, List<GH_Rectangle> courtR, List<GH_Box> auxiliary, List<Point3d> baseCenter, List<double> baseHalfX, List<double> baseHalfY)
        {
            GH_Box groupBoundary;
            if (toBeLayout is BasketballMatchBuilding)
            {
                buildings.Add((toBeLayout as BasketballMatchBuilding).groupBoundary);
                groupBoundary = (toBeLayout as BasketballMatchBuilding).groupBoundary;
                courtR.Add((toBeLayout as BasketballMatchBuilding).courtActual);
                baseBoundary.Add((toBeLayout as BasketballMatchBuilding).baseBoundary);
                ceilingBoundary.Add((toBeLayout as BasketballMatchBuilding).ceilingBoundary);
                baseCenter.Add((toBeLayout as BasketballMatchBuilding).baseCenter);
                for (int i = 0; i < (toBeLayout as BasketballMatchBuilding).seats.Count; i++)
                {
                    courtR.Add((toBeLayout as BasketballMatchBuilding).seats[i]);
                }
                for (int i = 0; i < (toBeLayout as BasketballMatchBuilding).auxiliary.Count; i++)
                {
                    auxiliary.Add((toBeLayout as BasketballMatchBuilding).auxiliary[i].auxiliaryUnit);
                }
            }
            else if (toBeLayout is GeneralCourtBuildingGroup)
            {
                buildings.Add((toBeLayout as GeneralCourtBuildingGroup).groupBoundary);
                groupBoundary = (toBeLayout as GeneralCourtBuildingGroup).groupBoundary;
                baseBoundary.Add((toBeLayout as GeneralCourtBuildingGroup).baseBoundary);
                ceilingBoundary.Add((toBeLayout as GeneralCourtBuildingGroup).ceilingBoundary);
                baseCenter.Add((toBeLayout as GeneralCourtBuildingGroup).baseCenter);
                for (int i = 0; i < (toBeLayout as GeneralCourtBuildingGroup).generalCourtGroup.courtOutline.Count; i++)
                {
                    courtR.Add((toBeLayout as GeneralCourtBuildingGroup).generalCourtGroup.courtOutline[i]);
                }
                if ((toBeLayout as GeneralCourtBuildingGroup).generalCourtAuxiliaryGroup != null)
                {
                    for (int i = 0; i < (toBeLayout as GeneralCourtBuildingGroup).generalCourtAuxiliaryGroup.auxiliary.Count; i++)
                    {
                        auxiliary.Add((toBeLayout as GeneralCourtBuildingGroup).generalCourtAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
                    }
                }
            }
            else if (toBeLayout is AquaticBuildingGroup)
            {
                buildings.Add((toBeLayout as AquaticBuildingGroup).groupBoundary);
                groupBoundary = (toBeLayout as AquaticBuildingGroup).groupBoundary;
                baseBoundary.Add((toBeLayout as AquaticBuildingGroup).baseBoundary);
                ceilingBoundary.Add((toBeLayout as AquaticBuildingGroup).ceilingBoundary);
                baseCenter.Add((toBeLayout as AquaticBuildingGroup).baseCenter);
                if ((toBeLayout as AquaticBuildingGroup).standardPool != null)
                {
                    courtR.Add((toBeLayout as AquaticBuildingGroup).standardPool.swimmingPool);
                }
                if ((toBeLayout as AquaticBuildingGroup).nonStandardPool != null)
                {
                    courtR.Add((toBeLayout as AquaticBuildingGroup).nonStandardPool.swimmingPool);
                }
                if ((toBeLayout as AquaticBuildingGroup).childrenPools != null)
                {
                    for (int i = 0; i < (toBeLayout as AquaticBuildingGroup).childrenPools.Count; i++)
                    {
                        courtR.Add((toBeLayout as AquaticBuildingGroup).childrenPools[i].swimmingPool);
                    }
                }
                if ((toBeLayout as AquaticBuildingGroup).aquaticBuildingAuxiliaryGroup != null)
                {
                    for (int i = 0; i < (toBeLayout as AquaticBuildingGroup).aquaticBuildingAuxiliaryGroup.auxiliary.Count; i++)
                    {
                        auxiliary.Add((toBeLayout as AquaticBuildingGroup).aquaticBuildingAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
                    }
                }
            }
            else if (toBeLayout is GymnasiumGroup)
            {
                buildings.Add((toBeLayout as GymnasiumGroup).groupBoundary);
                groupBoundary = (toBeLayout as GymnasiumGroup).groupBoundary;
                baseBoundary.Add((toBeLayout as GymnasiumGroup).baseBoundary);
                ceilingBoundary.Add((toBeLayout as GymnasiumGroup).ceilingBoundary);
                baseCenter.Add((toBeLayout as GymnasiumGroup).baseCenter);
                if ((toBeLayout as GymnasiumGroup).gymAuxiliaryGroup != null)
                {
                    for (int i = 0; i < (toBeLayout as GymnasiumGroup).gymAuxiliaryGroup.auxiliary.Count; i++)
                    {
                        auxiliary.Add((toBeLayout as GymnasiumGroup).gymAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
                    }
                }
            }
            else if (toBeLayout is Office)
            {
                buildings.Add((toBeLayout as Office).groupBoundary);
                groupBoundary = (toBeLayout as Office).groupBoundary;
                baseBoundary.Add((toBeLayout as Office).baseBoundary);
                ceilingBoundary.Add((toBeLayout as Office).ceilingBoundary);
                baseCenter.Add((toBeLayout as Office).baseCenter);
                for (int i = 0; i < (toBeLayout as Office).officeUnits.Count; i++)
                {
                    auxiliary.Add((toBeLayout as Office).officeUnits[i].officeUnit);
                }
            }
            else if (toBeLayout is Theater)
            {
                buildings.Add((toBeLayout as Theater).groupBoundary);
                groupBoundary = (toBeLayout as Theater).groupBoundary;
                baseBoundary.Add((toBeLayout as Theater).baseBoundary);
                ceilingBoundary.Add((toBeLayout as Theater).ceilingBoundary);
                auxiliary.Add((toBeLayout as Theater).hall);
                baseCenter.Add((toBeLayout as Theater).baseCenter);
                for (int i = 0; i < (toBeLayout as Theater).theaterUnits.Count; i++)
                {
                    auxiliary.Add((toBeLayout as Theater).theaterUnits[i].theaterUnit);
                }
            }
            else if (toBeLayout is LobbyUnit)
            {
                buildings.Add((toBeLayout as LobbyUnit).groupBoundary);
                groupBoundary = (toBeLayout as LobbyUnit).groupBoundary;
                baseBoundary.Add((toBeLayout as LobbyUnit).baseBoundary);
                ceilingBoundary.Add((toBeLayout as LobbyUnit).ceilingBoundary);
                baseCenter.Add((toBeLayout as LobbyUnit).baseCenter);
            }
            else
            {
                buildings.Add((toBeLayout as OtherFunction).groupBoundary);
                groupBoundary = (toBeLayout as OtherFunction).groupBoundary;
                baseBoundary.Add((toBeLayout as OtherFunction).baseBoundary);
                ceilingBoundary.Add((toBeLayout as OtherFunction).ceilingBoundary);
                baseCenter.Add((toBeLayout as OtherFunction).baseCenter);
                for (int i = 0; i < (toBeLayout as OtherFunction).functionUnits.Count; i++)
                {
                    auxiliary.Add((toBeLayout as OtherFunction).functionUnits[i].functionUnit);
                }
            }
            baseHalfX.Add(toBeLayout.GetHalfBaseSize(0, groupBoundary));
            baseHalfY.Add(toBeLayout.GetHalfBaseSize(1, groupBoundary));
        }
        //重绘显示网格
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (Manager.ifShrinkOk)
            {
                DisplayMaterial material = new DisplayMaterial();
                for (int i = 0; i < StaticObject.buildingPerFloor[0].Count; i++)
                {
                    if (StaticObject.buildingPerFloor[0][i] != null)
                    {
                        switch (Tool.GetBuildingType(StaticObject.buildingPerFloor[0][i]))
                        {
                            case BuildingType.篮球比赛馆:
                                material = new DisplayMaterial(System.Drawing.Color.Purple);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.篮球训练馆:
                                material = new DisplayMaterial(System.Drawing.Color.Orchid);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.游泳馆:
                                material = new DisplayMaterial(System.Drawing.Color.PowderBlue);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.羽毛球馆:
                                material = new DisplayMaterial(System.Drawing.Color.GreenYellow);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.网球馆:
                                material = new DisplayMaterial(System.Drawing.Color.LimeGreen);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.冰球馆:
                                material = new DisplayMaterial(System.Drawing.Color.LightSkyBlue);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.乒乓球馆:
                                material = new DisplayMaterial(System.Drawing.Color.Yellow);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.健身馆:
                                material = new DisplayMaterial(System.Drawing.Color.Pink);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.办公:
                                material = new DisplayMaterial(System.Drawing.Color.Brown);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.观演厅:
                                material = new DisplayMaterial(System.Drawing.Color.Purple);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.大厅:
                                material = new DisplayMaterial(System.Drawing.Color.White);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            case BuildingType.其他:
                                material = new DisplayMaterial(System.Drawing.Color.Gray);
                                args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildings[i].Value), material);
                                break;
                            default:
                                break;
                        }

                    }
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources._10_首层移动;

        public override Guid ComponentGuid
        {
            get { return new Guid("2D2D689F-154D-4300-A79C-66A338B81F42"); }
        }
    }
}