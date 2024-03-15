using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Management.Instrumentation;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using static System.Net.Mime.MediaTypeNames;

namespace Space_Layout
{
    public class UpperFloorMove : GH_Component
    {
        //二层及以上楼层建筑单体移动
        public UpperFloorMove()
          : base("上层建筑单体移动", "上层布局移动",
              "各场馆根据自身规则，向更优解移动",
               "建筑空间布局", "布局生成")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("首层布局是否完成", "运行状态", "", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("", "运行状态", "", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            #region 数据准备
            int isSuccess = 9;//生成状态，用于进程管理
            bool is1FOK = false;
            if (!DA.GetData(0, ref is1FOK)) return;
            #endregion

            //开始二层布局
            if ((is1FOK) && (!Manager.ifFinishLayout) && (Manager.restart != true))//若首层布局完成且二层及以上布局未完成
            {
                //遍历所有待布置楼层 StaticObject.buildingPerFloor.Count
                for (int i = Manager.currentFloor; i < StaticObject.buildingPerFloor.Count; i++)
                {
                    //若该层存在待布置场馆且未完成布局
                    if ((StaticObject.buildingPerFloor[i] != null) && (StaticObject.buildingPerFloor[i].Count != 0) && (Manager.ifUpFloorLayoutBegin[i]))
                    {
                        //若本层未进行初始化
                        if (Manager.ifUpFloorInitializeOk[i] == false)
                        {
                            #region 初始化该层场馆位置
                            //求本层障碍物边界
                            StaticObject.barrier[i] = (GetBarrierBoundary(i));
                            //根据面积对本层场馆放置顺序排序
                            List<int> sortOrder = SortAccordingLargeArea(i);
                            StaticObject.groundBoundaryIndex.Add(new List<int>());
                            for (int j = 0; j < sortOrder.Count; j++)//避免报错
                            {
                                StaticObject.groundBoundaryIndex[i].Add(-1);
                            }
                            //遍历该层所有待布置场馆
                            for (int j = 0; j < sortOrder.Count; j++)
                            {
                                //该场馆存在
                                if (StaticObject.buildingPerFloor[i][sortOrder[j]] != null)
                                {
                                    //查找与该场馆面积最为接近的下层场馆
                                    StaticObject.groundBoundaryIndex[i][sortOrder[j]] = GetAlmostSameAreaBuilding(i, sortOrder[j]);
                                }
                            }
                            //将该场馆布置与面积最接近场馆的中心位置
                            InitializeBuildingPosition(i, StaticObject.groundBoundaryIndex[i]);
                            //旋转新放置对象与nearest对象方向相同
                            for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                            {
                                //该场馆存在
                                if (StaticObject.buildingPerFloor[i][j] != null)
                                {
                                    //旋转新放置对象与nearest对象方向相同
                                    RotateToNearest(i, j, StaticObject.groundBoundaryIndex[i], j);
                                }
                            }
                            Manager.ifUpFloorInitializeOk[i] = true;
                            #endregion
                        }

                        #region 初始布局完成后，开始本层场馆移动
                        if (Manager.ifUpFloorInitializeOk[i] == true)//若本层初始化已完成
                        {
                            #region 计算障碍物相对位置【目前没用上】
                            List<List<int>> barrierPosition;//存放本层各场馆对障碍物的方位判断
                            List<List<double[,]>> barrierDistance;//存放本层各场馆对障碍物的方位判断
                            if ((StaticObject.barrier[i].Count != 0) && (StaticObject.barrier[i] != null))//若本层有障碍物
                            {
                                GetBarrierPositino(i, out barrierPosition, out barrierDistance);
                            }
                            #endregion

                            #region 判断是否存在场馆无法完全放入退线【会出现FAIL】
                            if ((Manager.ifUpFloorBoundaryForceOK[i] == false) && (Manager.restart != true))
                            {
                                for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                                {
                                    if (StaticObject.buildingPerFloor[i][j] != null)//若本层该对象不为空
                                    {
                                        if ((i % 2 == 0) && (j == 0))//12米标高大厅不算
                                        {
                                            continue;
                                        }
                                        else//奇数楼层
                                        {
                                            if (IfAlwaysBeyondBoundary(i, j) == false)//若存在无法在界内的情况
                                            {
                                                Manager.restart = true;
                                                Manager.runningTime++;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            #region 计算出界产生的力【会出现FAIL】
                            if ((Manager.ifUpFloorBoundaryForceOK[i] == false) && (Manager.restart != true))//未完成边界力拉回的环节
                            {
                                List<Vector3d> boundaryForce = new List<Vector3d>();//存放移入边界产生的力
                                bool ifBoundaryForceZero = false;//true=受力
                                //获取边界力
                                GetBoundaryForce(i, out boundaryForce);
                                //判断各场馆边界力均为0
                                foreach (var item in boundaryForce)
                                {
                                    if (item.Length != 0)
                                    {
                                        ifBoundaryForceZero = true;
                                    }
                                }
                                //若存在出界拉回的力
                                if (ifBoundaryForceZero)
                                {
                                    //查看是否完成此环节，若完成则直接进入障碍物相交检测
                                    Manager.ifUpFloorBoundaryForceOK[i] = IfFinishBoundaryForce(i, boundaryForce);
                                    //检测边界力拉回是否完成
                                    if (Manager.ifUpFloorBoundaryForceOK[i] == false)//若未完成，继续算边界力
                                    {
                                        //遍历本层各场馆
                                        for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                                        {
                                            //若该场馆受到出界拉回的力
                                            if ((boundaryForce[j] != null) && (boundaryForce[j] != Vector3d.Zero))
                                            {
                                                //场馆移动
                                                StaticObject.buildingPerFloor[i][j].Trans(Transform.Translation(boundaryForce[j]));
                                                StaticObject.baseCenterUpFloor[i][j] = StaticObject.buildingPerFloor[i][j].GetCenterPoint();
                                                Manager.boundaryCount++;
                                            }
                                            if (Manager.boundaryCount > 5)//若超过出界力回弹的限值
                                            {
                                                //粗暴地将其拉回界内
                                                ForcefullyPullIntoBoundary(i, j);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Manager.ifUpFloorBoundaryForceOK[i] = true;
                                }
                            }
                            #endregion

                            #region 避开已有障碍物【会出现FAIL】
                            //查看场馆与本层障碍物是否重叠，若重叠，则设法移开，若无法移开，则本轮生成失败
                            if ((Manager.ifUpFloorBoundaryForceOK[i] == true) && (Manager.ifUpFloorBarrierForceOK[i] == false) && (Manager.restart != true))//上一阶段出边界力已完成,本阶段障碍物力未完成
                            {
                                //用于判断是否完全不受障碍物力
                                List<bool> isBarrierForceZero = new List<bool>();
                                bool isForceZeroTotal = false;
                                //求解各场馆障碍物力
                                for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                                {
                                    if (StaticObject.buildingPerFloor[i][j] != null)
                                    {
                                        isBarrierForceZero.Add(AvoidBarrier(i, j, StaticObject.barrier[i]));
                                    }
                                }
                                //判断是否避开障碍物尝试次数超限，若超限，则本轮生成失败
                                if (Manager.barrierCount > StaticObject.barrierLimitCount * 2)
                                {
                                    Manager.restart = true;
                                    Manager.runningTime++;
                                    break;
                                }
                                //判断是否各场馆均不受障碍物力
                                foreach (var item in isBarrierForceZero)
                                {
                                    if (item == true)
                                    {
                                        isForceZeroTotal = true;
                                    }
                                }
                                if (isForceZeroTotal == false)//若不受障碍物力，完成次环节，进入下一环节
                                {
                                    Manager.ifUpFloorBarrierForceOK[i] = true;
                                }
                            }
                            #endregion

                            #region 计算场馆间相互遮挡产生的力
                            if ((Manager.ifUpFloorBarrierForceOK[i] == true) && (Manager.ifUpFloorPushForceOK[i] == false) && (Manager.restart != true))
                            {
                                //求各场馆间斥力
                                List<List<Vector3d>> vectorPush = new List<List<Vector3d>>();//各场馆所有推力变量
                                List<List<Vector3d>> vectorPushWithoutLobby = new List<List<Vector3d>>();//各场馆所有推力变量，Lobby不计入以换得更多有效结
                                List<List<Vector3d>> vectorPull = new List<List<Vector3d>>();//各场馆所有拉力变量
                                List<Vector3d> pushTotal = new List<Vector3d>();//各场馆推力的合力
                                List<List<int>> position = new List<List<int>>();//各场馆相对位置变量（i围绕j）,-1不相邻，0=正右，1=正下，2=正左，3=正上
                                List<List<int>> adjacent = new List<List<int>>();//各场馆毗邻关系（i围绕j）,-1不相邻，0=正右，1=正下，2=正左，3=正上
                                double moveLimit = 0.5;//小于次限制移动全长
                                double moveIndex = 0.5;//用以缩小场馆交叠时的单次移动量
                                int limitCount = 10;//计算次数上限值

                                for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                                {
                                    //数据初始化
                                    vectorPush.Add(new List<Vector3d>());
                                    vectorPushWithoutLobby.Add(new List<Vector3d>());
                                    vectorPull.Add(new List<Vector3d>());
                                    position.Add(new List<int>());
                                    adjacent.Add(new List<int>());
                                    //计算场馆间相对位置、所受推力
                                    if (StaticObject.buildingPerFloor[i][j] != null)//若本层该对象不为空
                                    {
                                        //获取场馆间相对位置
                                        GetBuildingPosition(i, j, position[j]);
                                        //若对象为大厅，则不计算压力，原地不动
                                        if (StaticObject.buildingTypesUpFloor[i][j] == BuildingType.大厅)
                                        {
                                            vectorPush[j].Add(Vector3d.Zero);
                                            vectorPushWithoutLobby[j].Add(Vector3d.Zero);
                                            pushTotal.Add(GetSumVector(vectorPush[j]));
                                        }
                                        //若对象不是大厅，计算场馆间推力
                                        else
                                        {
                                            for (int k = 0; k < StaticObject.buildingPerFloor[i].Count; k++)
                                            {
                                                if (StaticObject.buildingPerFloor[i][k] != null)
                                                {
                                                    //若是自身；
                                                    if (k == j)
                                                    {
                                                        vectorPush[j].Add(Vector3d.Zero);
                                                        vectorPushWithoutLobby[j].Add(Vector3d.Zero);
                                                    }
                                                    //若不是自身，计算场馆间推力
                                                    else
                                                    {
                                                        vectorPush[j].Add(GetOverlapVector(j, k, i));
                                                        if (StaticObject.buildingTypesUpFloor[i][k] == BuildingType.大厅)//去除与大厅间的受力
                                                        {
                                                            vectorPushWithoutLobby[j].Add(Vector3d.Zero);
                                                        }
                                                        else
                                                        {
                                                            vectorPushWithoutLobby[j].Add(GetOverlapVector(j, k, i));
                                                        }
                                                    }
                                                }
                                            }
                                            //判断移动后是否出界，是否与障碍物交叉
                                            //汇总单个场馆收到的推力
                                            if (Manager.ifPushModeChange == false)
                                            {
                                                pushTotal.Add(GetSumVector(vectorPush[j]));
                                            }
                                            else//超过计算上限时，切换模式
                                            {
                                                pushTotal.Add(GetSumVector(vectorPushWithoutLobby[j]));
                                            }

                                            //判断是否会移出边界
                                            Vector3d notOverBoundaryX = IfMoveOverBoundary(i, StaticObject.baseBoundaryUpFloor[i][j], new Vector3d(pushTotal[j].X, 0, 0));
                                            Vector3d notOverBoundaryY = IfMoveOverBoundary(i, StaticObject.baseBoundaryUpFloor[i][j], new Vector3d(0, pushTotal[j].Y, 0));
                                            if (((notOverBoundaryX.Length != 0) && (notOverBoundaryY.Length == 0)) || ((notOverBoundaryX.Length == 0) && (notOverBoundaryY.Length != 0)))//X、Y向有一方为0
                                            {
                                                if (notOverBoundaryX.Length == 0)//Y轴向移动
                                                {
                                                    //判断是否会与障碍物相交
                                                    notOverBoundaryY = IfIntersectWithBarrier(i, j, StaticObject.barrier[i], notOverBoundaryY);
                                                    //场馆移动
                                                    if (notOverBoundaryY.Length > moveLimit)
                                                    {
                                                        notOverBoundaryY *= moveIndex;
                                                    }
                                                    StaticObject.buildingPerFloor[i][j].Trans(Transform.Translation(notOverBoundaryY));
                                                    StaticObject.baseCenterUpFloor[i][j] = StaticObject.buildingPerFloor[i][j].GetCenterPoint();
                                                }
                                                else//X轴向移动
                                                {
                                                    //判断是否会与障碍物相交
                                                    notOverBoundaryX = IfIntersectWithBarrier(i, j, StaticObject.barrier[i], notOverBoundaryX);
                                                    //场馆移动
                                                    if (notOverBoundaryX.Length > moveLimit)
                                                    {
                                                        notOverBoundaryX *= moveIndex;
                                                    }
                                                    StaticObject.buildingPerFloor[i][j].Trans(Transform.Translation(notOverBoundaryX));
                                                    StaticObject.baseCenterUpFloor[i][j] = StaticObject.buildingPerFloor[i][j].GetCenterPoint();
                                                }
                                            }
                                            else//X/y均不为0或均为0
                                            {
                                                if ((notOverBoundaryX.Length <= notOverBoundaryY.Length) && (notOverBoundaryX.Length != 0))//X方向向量长度短，X轴向移动
                                                {
                                                    //判断是否会与障碍物相交
                                                    notOverBoundaryX = IfIntersectWithBarrier(i, j, StaticObject.barrier[i], notOverBoundaryX);
                                                    //场馆移动
                                                    if (notOverBoundaryX.Length > moveLimit)
                                                    {
                                                        notOverBoundaryX *= moveIndex;
                                                    }
                                                    StaticObject.buildingPerFloor[i][j].Trans(Transform.Translation(notOverBoundaryX));
                                                    StaticObject.baseCenterUpFloor[i][j] = StaticObject.buildingPerFloor[i][j].GetCenterPoint();
                                                }
                                                else//Y轴向移动
                                                {
                                                    notOverBoundaryY = IfIntersectWithBarrier(i, j, StaticObject.barrier[i], notOverBoundaryY);
                                                    //场馆移动
                                                    if (notOverBoundaryY.Length > moveLimit)
                                                    {
                                                        notOverBoundaryY *= moveIndex;
                                                    }
                                                    StaticObject.buildingPerFloor[i][j].Trans(Transform.Translation(notOverBoundaryY));
                                                    StaticObject.baseCenterUpFloor[i][j] = StaticObject.buildingPerFloor[i][j].GetCenterPoint();
                                                }
                                            }
                                        }
                                    }
                                }
                                //判断是否合力为0，是否可以开启Pull阶段
                                if (IfSumEqualZero(vectorPush))
                                {
                                    Manager.ifUpFloorPushForceOK[i] = true;
                                }
                                else
                                {
                                    Manager.pushCount++;
                                    if ((Manager.pushCount > StaticObject.pushLimitCount) && (Manager.pushCount < StaticObject.pushLimitCount * 3))//当超出限值统计次数时，不计大厅产生的推力再尝试若干次
                                    {
                                        Manager.ifPushModeChange = true;
                                        if (IfSumEqualZero(vectorPushWithoutLobby))//不计大厅的合力为0
                                        {
                                            Manager.ifUpFloorPushForceOK[i] = true;
                                        }
                                    }
                                    else if (Manager.pushCount > StaticObject.pushLimitCount * 3.5)//当超出限值统计次数时，若不计大厅能推力合力为0也可以
                                    {
                                        if (IfSumEqualZero(vectorPushWithoutLobby))//不计大厅的合力为0
                                        {
                                            Manager.ifUpFloorPushForceOK[i] = true;
                                        }
                                        else
                                        {
                                            Manager.restart = true;
                                            Manager.runningTime++;
                                            break;
                                        }
                                    }
                                }
                            }
                            #endregion

                            #region 场馆间拉力,接近大厅的场馆向大厅靠拢
                            if ((Manager.ifUpFloorPushForceOK[i] == true) && (Manager.ifUpFloorPullForceOK[i] == false) && (Manager.restart != true))
                            {
                                for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                                {
                                    if (StaticObject.buildingPerFloor[i][j] != null)//若本层该对象不为空
                                    {
                                        //数据准备
                                        int lobbyPosition = -2;//场馆与大厅关系，初始值-2
                                        bool ifIntersect = false;//场馆与大厅是否相交
                                        double detaX = 0;//场馆距lobby的X方向的间距
                                        double detaY = 0;//场馆距lobby的Y方向的间距

                                        if (StaticObject.buildingTypesUpFloor[i][j] != BuildingType.大厅)//对象不是大厅
                                        {
                                            #region 判断大厅在什么方位
                                            if (i % 2 == 0)//偶数层
                                            {
                                                //第一个场馆是大厅
                                                lobbyPosition = GetSpecificBuildingPosition(i, j, 0);
                                                //场馆距lobby的X方向的间距
                                                detaX = Math.Abs((StaticObject.baseCenterUpFloor[i][j] - StaticObject.baseCenterUpFloor[i][0]).X) - StaticObject.baseHalfXUpFloor[i][j] - StaticObject.baseHalfXUpFloor[i][0];
                                                //场馆距lobby的Y方向的间距
                                                detaY = Math.Abs((StaticObject.baseCenterUpFloor[i][j] - StaticObject.baseCenterUpFloor[i][0]).Y) - StaticObject.baseHalfYUpFloor[i][j] - StaticObject.baseHalfYUpFloor[i][0];
                                                //场馆与大厅是否相交
                                                ifIntersect = IfIntersect(StaticObject.baseBoundaryUpFloor[i][j], StaticObject.baseBoundaryUpFloor[i][0]);
                                            }
                                            else//奇数层
                                            {
                                                if (i == 1)//位于6米层高处，调用0米层高处大厅
                                                {
                                                    //第一个障碍物是大厅
                                                    lobbyPosition = GetSpecificBuildingPosition(i, j, StaticObject.baseBoundary[0]);
                                                    //求与大厅间距
                                                    //场馆距lobby的X方向的间距
                                                    detaX = Math.Abs((StaticObject.baseCenterUpFloor[i][j] - StaticObject.baseCenter[0]).X) - StaticObject.baseHalfXUpFloor[i][j] - StaticObject.baseHalfX[0];
                                                    //场馆距lobby的Y方向的间距
                                                    detaY = Math.Abs((StaticObject.baseCenterUpFloor[i][j] - StaticObject.baseCenter[0]).Y) - StaticObject.baseHalfYUpFloor[i][j] - StaticObject.baseHalfY[0];
                                                    //场馆与大厅是否相交
                                                    Curve lobbyCurve = StaticObject.baseBoundary[0].DuplicateCurve();
                                                    lobbyCurve.Transform(Transform.Translation(Vector3d.ZAxis * StaticObject.standardFloorHeight));//获取1层标高大厅的轮廓线
                                                    ifIntersect = IfIntersect(StaticObject.baseBoundaryUpFloor[i][j], lobbyCurve);
                                                }
                                                else//位于12米+层高处，调用12米层高处大厅
                                                {
                                                    //第一个障碍物是大厅
                                                    lobbyPosition = GetSpecificBuildingPosition(i, j, StaticObject.baseBoundaryUpFloor[i - 1][0]);
                                                    //求与大厅间距
                                                    //场馆距lobby的X方向的间距
                                                    detaX = Math.Abs((StaticObject.baseCenterUpFloor[i][j] - StaticObject.baseCenterUpFloor[i - 1][0]).X) - StaticObject.baseHalfXUpFloor[i][j] - StaticObject.baseHalfXUpFloor[i - 1][0];
                                                    //场馆距lobby的Y方向的间距
                                                    detaY = Math.Abs((StaticObject.baseCenterUpFloor[i][j] - StaticObject.baseCenterUpFloor[i - 1][0]).Y) - StaticObject.baseHalfYUpFloor[i][j] - StaticObject.baseHalfYUpFloor[i - 1][0];
                                                    //场馆与大厅是否相交
                                                    Curve lobbyCurve = StaticObject.baseBoundaryUpFloor[i - 1][0].DuplicateCurve();
                                                    lobbyCurve.Transform(Transform.Translation(Vector3d.ZAxis * StaticObject.standardFloorHeight));//获取下层标高大厅的轮廓线
                                                    ifIntersect = IfIntersect(StaticObject.baseBoundaryUpFloor[i][j], lobbyCurve);
                                                }
                                            }
                                            #endregion

                                            #region 场馆是否需要向大厅靠拢，若需要，则移动
                                            if (ifIntersect != true)//场馆与大厅不想交
                                            {
                                                Vector3d move = ifIsTheNearestBuilding(i, j, detaX, detaY, lobbyPosition, ifIntersect);
                                                if ((move.Length != 0) && (move.Length <= 20))
                                                {
                                                    StaticObject.buildingPerFloor[i][j].Trans(Transform.Translation(move));
                                                    StaticObject.baseCenterUpFloor[i][j] = StaticObject.buildingPerFloor[i][j].GetCenterPoint();
                                                }
                                                else if (move.Length > 20)//移动距离超长是，验证下部是否未超出太多
                                                {
                                                    move = IfCanStillPull(i, j, StaticObject.floorBoundary[i], StaticObject.groundBoundaryIndex[i], move);
                                                    StaticObject.buildingPerFloor[i][j].Trans(Transform.Translation(move));
                                                    StaticObject.baseCenterUpFloor[i][j] = StaticObject.buildingPerFloor[i][j].GetCenterPoint();
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                }

                                //本阶段开关
                                Manager.ifUpFloorPullForceOK[i] = true;
                            }
                            #endregion
                        }
                        #endregion

                        //本层布局完成，调整层间布局开关
                        if ((Manager.ifUpFloorBoundaryForceOK[i] == true) && (Manager.ifUpFloorBarrierForceOK[i] == true) && (Manager.ifUpFloorPushForceOK[i] == true) && (Manager.ifUpFloorPullForceOK[i] == true) && (Manager.restart != true))
                        {
                            if (i < StaticObject.buildingPerFloor.Count - 1)//非最后一层
                            {
                                Manager.ifUpFloorLayoutBegin[i] = false;
                                Manager.ifUpFloorLayoutBegin[i + 1] = true;
                            }
                            else//最后一层
                            {
                                Manager.ifUpFloorLayoutBegin[i] = false;
                                Manager.ifFinishLayout = true;//布局结束
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    //若本层没有建筑，等同于完成本层布局，更新所有参数
                    else
                    {
                        Manager.ifUpFloorLayoutBegin[i] = false;
                        Manager.ifUpFloorInitializeOk[i] = true;
                        Manager.ifUpFloorBoundaryForceOK[i] = true;
                        Manager.ifUpFloorBarrierForceOK[i] = true;
                        Manager.ifUpFloorPushForceOK[i] = true;
                        Manager.ifUpFloorPullForceOK[i] = true;
                        StaticObject.groundBoundaryIndex.Add(new List<int>());
                        if (i < StaticObject.buildingPerFloor.Count - 1)//若不是最后一层，开启下一层
                        {
                            Manager.ifUpFloorLayoutBegin[i + 1] = true;
                        }
                        else//若为最后一层，布局结束
                        {
                            Manager.ifFinishLayout = true;
                        }
                    }
                }

            }

            //进行到此处，二层及以上布局成功
            if (Manager.ifFinishLayout == true)
            {
                isSuccess = 1;
            }
            else if ((Manager.restart == true) || (Manager.upFloorCount > 50))
            {
                isSuccess = 2;
            }
            else if (isSuccess == 9)
            {
                //统计二层移动总计算次数
                Manager.upFloorCount++;
            }
            else
            {
                isSuccess = 9;
            }

            #region 数据输出
            DA.SetData(0, isSuccess);
            #endregion
        }

        //根据面积排序场馆放置位置的计算顺序
        public List<int> SortAccordingLargeArea(int floor)
        {
            List<int> sortOrder = new List<int>();
            List<double> area = new List<double>();
            int largestOne = -1;
            //计算本层待布置建筑的面积
            for (int j = 0; j < StaticObject.buildingPerFloor[floor].Count; j++)
            {
                if (StaticObject.buildingPerFloor[floor][j] != null)
                {
                    AreaMassProperties compute = AreaMassProperties.Compute(StaticObject.baseBoundaryUpFloor[floor][j]);
                    area.Add(compute.Area);
                }
            }
            //排序前的顺序
            for (int j = 0; j < StaticObject.buildingPerFloor[floor].Count; j++)
            {
                if (StaticObject.buildingPerFloor[floor][j] != null)
                {
                    sortOrder.Add(j);
                }
            }
            //根据面积排序场馆放置先后
            sortOrder.Sort(new DoubleCompare(area));
            //12米标高将大厅放置在首位
            if (floor == 2)
            {
                sortOrder.Remove(0);
                sortOrder.Insert(0, 0);
            }
            return sortOrder;
        }
        //查询与当前场馆面积最为接近的下层对象
        public int GetAlmostSameAreaBuilding(int floor, int item)
        {
            List<double> areaDifference = new List<double>();
            AreaMassProperties compute = AreaMassProperties.Compute(StaticObject.baseBoundaryUpFloor[floor][item]);//下层该对象底面积
            for (int i = 0; i < StaticObject.AvailableArea[floor].Count; i++)
            {
                areaDifference.Add(StaticObject.AvailableArea[floor][i] - compute.Area);
            }
            int nearest = -1;//与item底面积最为接近的对象编号
            if ((floor == 2) && (item == 0))
            {
                nearest = 0;
                StaticObject.AvailableArea[floor][nearest] -= compute.Area;//基底面积去除已占用部分
            }
            else//item不是二层的大厅
            {
                for (int i = 0; i < areaDifference.Count; i++)
                {
                    if (areaDifference[i] >= 0)//正常情况，边界基底要大于item的基底面积
                    {
                        if (nearest == -1)//首次运算
                        {
                            nearest = i;
                        }
                        else
                        {
                            if (areaDifference[i] > areaDifference[nearest])
                            {
                                nearest = i;
                            }
                        }
                    }
                }
                if (nearest != -1)
                {
                    StaticObject.AvailableArea[floor][nearest] -= compute.Area;//基底面积去除已占用部分
                }
                else//遍历完所有下层对象，没有找到比现在大的
                {
                    for (int i = 0; i < areaDifference.Count; i++)
                    {
                        if (nearest == -1)//首次运算
                        {
                            nearest = i;
                        }
                        else
                        {
                            if (areaDifference[i] > areaDifference[nearest])
                            {
                                nearest = i;
                            }
                        }
                    }
                    StaticObject.AvailableArea[floor][nearest] -= compute.Area;//基底面积去除已占用部分
                }
            }
            return nearest;
        }
        //初次布置场馆位置至指定对象中心
        public void InitializeBuildingPosition(int floor, List<int> nearest)
        {
            for (int i = 0; i < nearest.Count; i++)
            {
                //对象移动到面积最适宜的下层场馆中心
                Vector3d moveToPosition = StaticObject.floorBoundary[floor][nearest[i]].GetBoundingBox(true).Center - StaticObject.baseBoundaryUpFloor[floor][i].GetBoundingBox(true).Center;
                StaticObject.buildingPerFloor[floor][i].Trans(Transform.Translation(moveToPosition));
                //由于中心点移动出现BUG，再次对中心点
                StaticObject.baseCenterUpFloor[floor][i] = StaticObject.buildingPerFloor[floor][i].GetCenterPoint();
            }
        }
        //旋转新放置对象与nearest对象方向相同
        public void RotateToNearest(int floor, int item, List<int> nearest, int nearestItem)//item=新放置对象，
        {
            //获取item的朝向（水平/垂直）
            Orientation orientationItem = new Orientation();
            if (StaticObject.baseHalfXUpFloor[floor][item] < StaticObject.baseHalfYUpFloor[floor][item])
            {
                orientationItem = Orientation.垂直;
            }
            else
            {
                orientationItem = Orientation.水平;
            }
            //获取nearest的朝向（水平/垂直）
            Orientation orientationNearest = new Orientation();
            double xMax = StaticObject.floorBoundary[floor][nearest[nearestItem]].GetBoundingBox(true).Max.X;
            double xMin = StaticObject.floorBoundary[floor][nearest[nearestItem]].GetBoundingBox(true).Min.X;
            double deltaX = xMax - xMin;
            double yMax = StaticObject.floorBoundary[floor][nearest[nearestItem]].GetBoundingBox(true).Max.Y;
            double yMin = StaticObject.floorBoundary[floor][nearest[nearestItem]].GetBoundingBox(true).Min.Y;
            double deltaY = yMax - yMin;
            if (deltaX < deltaY)
            {
                orientationNearest = Orientation.垂直;
            }
            else
            {
                orientationNearest = Orientation.水平;
            }
            //当新放置对象与nearest对象方向不同时，旋转新放置物体
            if (orientationItem != orientationNearest)
            {
                StaticObject.buildingPerFloor[floor][item].MustRotate(Tool.AngleToRadians(90));
                //更新数据
                double tempHalf = StaticObject.baseHalfXUpFloor[floor][item];
                StaticObject.baseHalfXUpFloor[floor][item] = StaticObject.baseHalfYUpFloor[floor][item];
                StaticObject.baseHalfYUpFloor[floor][item] = tempHalf;
                StaticObject.buildingPerFloor[floor][item].SetOrientation();
            }
        }
        //获取本层障碍物边界
        public List<Curve> GetBarrierBoundary(int currentFloor)
        {
            List<Curve> barrierLine = new List<Curve>();
            Point3d[] barrierPoint;//存储相交点

            //剖切位置标高：本层标高之上多少米
            double addClippingHeight = 0.5;

            //获取各层与剖切平面的交线
            for (int i = 0; i < currentFloor; i++)
            {
                //若该层有建筑
                if ((StaticObject.buildingPerFloor[i] != null) && (StaticObject.buildingPerFloor[currentFloor].Count != 0))
                {
                    //新建剖切平面坐标点
                    Point3d planeCenter = new Point3d(StaticObject.baseCenterUpFloor[currentFloor][0].X, StaticObject.baseCenterUpFloor[currentFloor][0].Y, currentFloor * StaticObject.standardFloorHeight + addClippingHeight);
                    //新建剖切平面
                    Plane clippingPlane = new Plane(planeCenter, Vector3d.ZAxis);
                    //遍历本层建筑
                    for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                    {
                        //若该建筑不为null且不是大厅
                        if ((StaticObject.buildingPerFloor[i][j] != null) && (Tool.GetBuildingType(StaticObject.buildingPerFloor[i][j]) != BuildingType.大厅))
                        {
                            //求是否产生barrier
                            Brep test = StaticObject.buildingPerFloor[i][j].GetBoundaryBox().Value.ToBrep();
                            //存储障碍物原始交线
                            Curve[] testCurve;
                            //若相交产生障碍物交线
                            if (Rhino.Geometry.Intersect.Intersection.BrepPlane(test, clippingPlane, 0, out testCurve, out barrierPoint))
                            {
                                //考虑相交虽成功，但不产生交线的BUG情况，进行交线有效性判定
                                if (testCurve.Length != 0)
                                {
                                    //由于平面与brep的交线为nurbscurve，无法转换为gh_curve，所以要走其他路径重新构建交线，尝试后采用polycurve
                                    //存放交点
                                    List<Point3d> points = new List<Point3d>();
                                    //从交线上抽离角点，并踢出重复点
                                    for (int k = 0; k < 4; k++)
                                    {
                                        Point3d start = testCurve[k].PointAtStart;
                                        points.Add(start);
                                    }
                                    //由于makeclosed在不加点情况下无效，所以手动添加第0个顶点
                                    points.Add(testCurve[0].PointAtStart);
                                    //构建交线的polyline
                                    PolylineCurve tempPolyline = new PolylineCurve(points);
                                    //闭合交线
                                    tempPolyline.MakeClosed(100000);
                                    double moveToCurrentLevel = tempPolyline.GetBoundingBox(true).Center.Z - currentFloor * StaticObject.standardFloorHeight;
                                    tempPolyline.Transform(Transform.Translation(Vector3d.ZAxis * (-moveToCurrentLevel)));
                                    //添加至交线集合
                                    barrierLine.Add(tempPolyline);
                                }
                            }
                        }
                    }
                }
            }
            return barrierLine;
        }
        //获取障碍物相对场馆的位置
        public void GetBarrierPositino(int currentFloor, out List<List<int>> barrierPosition, out List<List<double[,]>> barrierDistance)
        {
            barrierPosition = new List<List<int>>();
            barrierDistance = new List<List<double[,]>>();
            //遍历本层各场馆
            for (int i = 0; i < StaticObject.baseBoundaryUpFloor[currentFloor].Count; i++)
            {
                //初始化
                barrierPosition.Add(new List<int>());
                barrierDistance.Add(new List<double[,]>());
                for (int j = 0; j < StaticObject.barrier[currentFloor].Count; j++)
                {
                    //获取障碍物参数
                    double xBarrier = StaticObject.barrier[currentFloor][j].GetBoundingBox(true).Center.X;
                    double yBarrier = StaticObject.barrier[currentFloor][j].GetBoundingBox(true).Center.Y;
                    double barrierXHalf = (StaticObject.barrier[currentFloor][j].GetBoundingBox(true).Max.X - StaticObject.barrier[currentFloor][j].GetBoundingBox(true).Min.X) * 0.5;
                    double barrierYHalf = (StaticObject.barrier[currentFloor][j].GetBoundingBox(true).Max.Y - StaticObject.barrier[currentFloor][j].GetBoundingBox(true).Min.Y) * 0.5;
                    //计算场馆与障碍物最小间距
                    double deltaXmin = barrierXHalf + StaticObject.baseHalfXUpFloor[currentFloor][i];
                    double deltaYmin = barrierYHalf + StaticObject.baseHalfYUpFloor[currentFloor][i];
                    //用于存放场馆与障碍物的实际距离
                    double xDistance;
                    double yDistance;

                    //场馆在障碍物 右侧
                    if ((StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier >= deltaXmin) && (Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) < deltaYmin))
                    {
                        barrierPosition[i].Add(0);

                    }
                    //场馆在障碍物 右上侧
                    else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier >= deltaXmin) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) >= deltaYmin)
                    {
                        barrierPosition[i].Add(-2);
                    }
                    //场馆在障碍物 右下侧
                    else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier >= deltaXmin) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) <= -deltaYmin)
                    {
                        barrierPosition[i].Add(-2);
                    }
                    //场馆在障碍物 下侧
                    else if ((Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier) < deltaXmin) && ((StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) <= -deltaYmin))
                    {
                        barrierPosition[i].Add(1);
                    }
                    //场馆在障碍物 左下侧
                    else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier <= -deltaXmin) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) <= -deltaYmin)
                    {
                        barrierPosition[i].Add(-2);
                    }
                    //场馆在障碍物 左侧
                    else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier <= -deltaXmin) && (Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) < deltaYmin))
                    {
                        barrierPosition[i].Add(2);
                    }
                    //场馆在障碍物 左上侧
                    else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier <= -deltaXmin) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) >= deltaYmin)
                    {
                        barrierPosition[i].Add(-2);
                    }
                    //场馆在障碍物 上侧
                    else if ((Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier) < deltaXmin) && ((StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) >= deltaYmin))
                    {
                        barrierPosition[i].Add(3);
                    }
                    //场馆与障碍物相交 
                    else if ((Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].X - xBarrier) < deltaXmin) && (Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].Y - yBarrier) < deltaYmin))
                    {
                        barrierPosition[i].Add(-1);
                    }
                    //以防万一的其他情况
                    else
                    {
                        barrierPosition[i].Add(-3);
                    }
                    //获取距离障碍物的距离
                    xDistance = Math.Abs(xBarrier - StaticObject.baseCenterUpFloor[currentFloor][i].X) - deltaXmin;
                    yDistance = Math.Abs(yBarrier - StaticObject.baseCenterUpFloor[currentFloor][i].Y) - deltaYmin;
                    //存储至障碍物距离list
                    barrierDistance[i].Add(new double[2, 1] { { xDistance }, { yDistance } });
                }
            }
        }
        //获取突出边界产生的力
        public void GetBoundaryForce(int currentFloor, out List<Vector3d> boundaryForce)
        {
            //数据初始化
            boundaryForce = new List<Vector3d>();
            //获取本层标高的基底边界
            Curve testBoundary = GetCurrentLevelBoundary(currentFloor);

            //遍历本层各场馆
            for (int i = 0; i < StaticObject.baseBoundaryUpFloor[currentFloor].Count; i++)
            {
                #region 数据准备
                //求场馆与边界的相交情况
                CurveIntersections intersect = Intersection.CurveCurve(testBoundary, StaticObject.baseBoundaryUpFloor[currentFloor][i], 0.1, 0.1);
                List<Point3d> vertices = GetVertices(currentFloor, StaticObject.baseBoundaryUpFloor[currentFloor][i]);//求待测曲线4个顶点
                List<Point3d> verticesOutside = new List<Point3d>();//出界的场馆顶点
                List<Point3d> crossPt = new List<Point3d>();//相交点
                int outsideCount = 0;//出界点
                Plane plane = new Plane();//边界所在平面
                testBoundary.TryGetPlane(out plane);//获取边界平面
                double maxError = 0.001;//可忽视的最小误差
                #endregion

                if (intersect.Count > 1)//如果交点≥2
                {
                    //获取所有相交点
                    for (int k = 0; k < intersect.Count; k++)
                    {
                        crossPt.Add(intersect[k].PointA);
                    }

                    //检测是否有点出界
                    for (int j = 0; j < 4; j++)
                    {
                        PointContainment ptContain = testBoundary.Contains(vertices[j], plane, 0);
                        if (ptContain == PointContainment.Outside)//若场馆边界点出界了,累加出界次数
                        {
                            outsideCount++;
                            verticesOutside.Add(vertices[j]);//添加出界点
                        }
                    }

                    #region 计算出界拉回的力
                    //1个点出界
                    if (outsideCount == 1)
                    {
                        List<Vector3d> testMove = new List<Vector3d>();//存放临时出界拉回力
                        for (int m = 0; m < crossPt.Count; m++)//遍历所有交点
                        {
                            testMove.Add(crossPt[m] - verticesOutside[0]);
                        }
                        boundaryForce.Add(GetShortestVector(testMove));//返回模数最小的向量
                    }
                    //2个点出界
                    else if (outsideCount == 2)
                    {
                        List<Vector3d> testMove = new List<Vector3d>();//记录出边后各点的位移
                        for (int m = 0; m < crossPt.Count; m++)//遍历所有交点
                        {
                            for (int n = 0; n < verticesOutside.Count; n++)//遍历所有出界点
                            {
                                if ((Math.Abs(crossPt[m].X - verticesOutside[n].X) < maxError) || (Math.Abs(crossPt[m].Y - verticesOutside[n].Y) < maxError))//当交叉点与outsideCountX或Y轴相同，则为有效变量
                                {
                                    testMove.Add(crossPt[m] - verticesOutside[n]);
                                }
                            }
                        }
                        boundaryForce.Add(GetLongestVector(testMove));//返回模数最长的向量
                    }
                    else if (outsideCount == 0)//点卡在边界上，没有出边
                    {
                        boundaryForce.Add(Vector3d.Zero);
                    }
                    //3个点出界
                    else
                    {
                        for (int m = 0; m < verticesOutside.Count; m++)//遍历所有出界点
                        {
                            List<Vector3d> testMove = new List<Vector3d>();//记录出边后各点的位移
                            for (int n = 0; n < crossPt.Count; n++)//遍历所有交点
                            {
                                if (Math.Abs(crossPt[n].Y - verticesOutside[m].Y) < maxError)//X轴同高时无效
                                {
                                    testMove = new List<Vector3d>();
                                    break;
                                }
                                else if (Math.Abs(crossPt[n].X - verticesOutside[m].X) < maxError)//Y轴同高时无效
                                {
                                    testMove = new List<Vector3d>();
                                    break;
                                }
                                else//X轴、Y轴均不同高
                                {
                                    testMove.Add(crossPt[n] - verticesOutside[m]);
                                }
                            }
                            //筛选有效移动变量
                            if ((testMove != null) && (testMove.Count != 0))
                            {
                                if (StaticObject.baseHalfXUpFloor[currentFloor][i] >= StaticObject.baseHalfYUpFloor[currentFloor][i])//场馆X轴长
                                {
                                    if (testMove[0].X >= testMove[1].X)
                                    {
                                        boundaryForce.Add(testMove[0]);//取X轴最长的向量
                                    }
                                    else
                                    {
                                        boundaryForce.Add(testMove[1]);//取X轴最长的向量
                                    }
                                }
                                else//场馆Y轴长
                                {
                                    if (testMove[0].Y >= testMove[1].Y)
                                    {
                                        boundaryForce.Add(testMove[0]);//取Y轴最长的向量
                                    }
                                    else
                                    {
                                        boundaryForce.Add(testMove[1]);//取Y轴最长的向量
                                    }
                                }
                            }
                            else//【后加的，可能有问题】
                            {
                                boundaryForce.Add(Vector3d.Zero);
                            }
                        }
                    }
                    #endregion
                }
                else//场馆与退线交点＜2
                {
                    boundaryForce.Add(Vector3d.Zero);
                }
            }
        }
        //超过出界力运行次数时，强制拉回退线内
        public void ForcefullyPullIntoBoundary(int currentFloor, int item)
        {
            Point3d itemCenter = StaticObject.baseCenterUpFloor[currentFloor][item];
            Point3d boundaryCenter = SiteInfo.siteRetreat.Boundingbox.Center;
            double moveDistance = 2;
            if (itemCenter.Y >= boundaryCenter.Y)
            {
                StaticObject.buildingPerFloor[currentFloor][item].Trans(Transform.Translation(Vector3d.YAxis * (-moveDistance)));
                StaticObject.baseCenterUpFloor[currentFloor][item] = StaticObject.buildingPerFloor[currentFloor][item].GetCenterPoint();
                Manager.boundaryCount = 0;
            }
            else
            {
                StaticObject.buildingPerFloor[currentFloor][item].Trans(Transform.Translation(Vector3d.YAxis * moveDistance));
                StaticObject.baseCenterUpFloor[currentFloor][item] = StaticObject.buildingPerFloor[currentFloor][item].GetCenterPoint();
                Manager.boundaryCount = 0;
            }
        }
        //获取曲线的4个顶点，用于认不出顶点的矩形
        public List<Point3d> GetVertices(int currentFloor, Curve test)
        {
            List<Point3d> vertices = new List<Point3d>();
            BoundingBox testBox = test.GetBoundingBox(true);
            vertices.Add(testBox.Min);
            vertices.Add(testBox.Max);
            vertices.Add(new Point3d(testBox.Min.X, testBox.Max.Y, currentFloor * StaticObject.standardFloorHeight));
            vertices.Add(new Point3d(testBox.Max.X, testBox.Min.Y, currentFloor * StaticObject.standardFloorHeight));
            return vertices;
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
        //获取模数最长的向量
        public Vector3d GetShortestVector(List<Vector3d> testMove)
        {
            Vector3d temp = testMove[0];
            foreach (Vector3d v in testMove)
            {
                if (v.Length < temp.Length) { temp = v; }
            }
            return temp;
        }
        //判断此场馆该方向无论如何摆放也会出界
        public bool IfAlwaysBeyondBoundary(int currentFloor, int index)
        {
            //获取本层标高的基底边界
            Curve testBoundary = GetCurrentLevelBoundary(currentFloor);
            //获取场馆生线的点
            Point3d basePoint = StaticObject.baseCenterUpFloor[currentFloor][index];
            Point3d start;
            Point3d end;
            double deltaDistance = 2000;
            Line crossLine;

            //生成将与testBoundary求交点的直线
            if (StaticObject.baseHalfXUpFloor[currentFloor][index] >= StaticObject.baseHalfYUpFloor[currentFloor][index])//若X轴长于Y轴
            {
                start = basePoint - Vector3d.XAxis * deltaDistance;
                end = basePoint + Vector3d.XAxis * deltaDistance;
                crossLine = new Line(start, end);
            }
            else//若Y轴长于X轴
            {
                start = basePoint - Vector3d.YAxis * deltaDistance;
                end = basePoint + Vector3d.YAxis * deltaDistance;
                crossLine = new Line(start, end);
            }

            //求crossLine与testBoundary的交线
            CurveIntersections intersect = Intersection.CurveCurve(testBoundary, crossLine.ToNurbsCurve(), 0.1, 0.1);

            double minLength=0;
            if (intersect.Count>0)
            {
                Line segment = new Line(intersect[0].PointA, intersect[1].PointA);
                minLength = segment.Length;
            }
            //判断是否该场馆无法在界内
            if (StaticObject.baseHalfXUpFloor[currentFloor][index] >= StaticObject.baseHalfYUpFloor[currentFloor][index])//若X轴长于Y轴
            {
                if (minLength < StaticObject.baseHalfXUpFloor[currentFloor][index] * 2)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else//若Y轴长于X轴
            {
                if (minLength < StaticObject.baseHalfYUpFloor[currentFloor][index] * 2)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
        //获取首层用地退线
        public Curve GetCurrentLevelBoundary(int currentFloor)
        {
            //获取本层标高的基底边界
            GH_Curve testBoundary = (GH_Curve)SiteInfo.siteRetreat.Duplicate();
            testBoundary.Transform(Transform.Translation(Vector3d.ZAxis * StaticObject.standardFloorHeight * currentFloor));
            return testBoundary.Value;
        }
        //获取场馆相对位置
        public void GetBuildingPosition(int currentFloor, int index, List<int> position)
        {
            for (int j = 0; j < StaticObject.buildingPerFloor[currentFloor].Count; j++)
            {
                if (StaticObject.buildingPerFloor[currentFloor][j] != null)//若本层有场馆
                {
                    if (j != index)//待测场馆不是自身
                    {
                        double detaX = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].X - StaticObject.baseCenterUpFloor[currentFloor][index].X);
                        double distanceXMin = StaticObject.baseHalfXUpFloor[currentFloor][j] + StaticObject.baseHalfXUpFloor[currentFloor][index];
                        double detaY = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].Y - StaticObject.baseCenterUpFloor[currentFloor][index].Y);
                        double distanceYMin = StaticObject.baseHalfYUpFloor[currentFloor][j] + StaticObject.baseHalfYUpFloor[currentFloor][index];
                        //获取其他建筑相对位置
                        position.Add(GetOtherBuildingPosition(detaX, distanceXMin, detaY, distanceYMin, currentFloor, index, j));
                    }
                    else//j是自身时标记-1
                    {
                        position.Add(-1);
                    }
                }
            }
        }
        //获取其他建筑相对位置关系:【上偏右】11；【右偏上】4；【右】0；【右偏下】5；【下偏右】6；【下】1；【下偏左】7；【左偏下】8；【左】2；【左偏上】9；【上偏左】10；【上】3
        public int GetOtherBuildingPosition(double detaX, double distanceXMin, double detaY, double distanceYMin, int currentFloor, int i, int j)
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
                if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 4; }
                    else { return 11; }
                }
                //i在j正右上区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y >= StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 0; }
                //i在j正右下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 0; }
                //i在j右下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 5; }
                    else { return 6; }
                }
                //i在j正下右区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X >= StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                { return 1; }
                //i在j正下左区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                { return 1; }
                //i在j左下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 8; }
                    else { return 7; }
                }
                //i在j正左下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y <= StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 2; }
                //i在j正左上区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 2; }
                //i在j左上区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY >= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (moveX > moveY) { return 9; }
                    else { return 10; }
                }
                //i在j正上左区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X <= StaticObject.baseCenterUpFloor[currentFloor][j].X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > StaticObject.baseCenterUpFloor[currentFloor][j].Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
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
                if ((StaticObject.baseCenterUpFloor[currentFloor][j].X < StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j右上
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].Y < StaticObject.baseCenterUpFloor[currentFloor][i].Y)
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
                else if ((StaticObject.baseCenterUpFloor[currentFloor][j].Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y) && (detaX < detaY))
                {
                    //i在j下右
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].X < StaticObject.baseCenterUpFloor[currentFloor][i].X)
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
                else if ((StaticObject.baseCenterUpFloor[currentFloor][j].X > StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j左下
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y)
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
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].X > StaticObject.baseCenterUpFloor[currentFloor][i].X)
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
                if ((StaticObject.baseCenterUpFloor[currentFloor][j].X < StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j右上
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].Y < StaticObject.baseCenterUpFloor[currentFloor][i].Y)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 3; }
                        else
                        { return 0; }
                    }
                    else//i在j右下
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 1; }
                        else
                        { return 0; }
                    }
                }
                //i在j下侧
                else if ((StaticObject.baseCenterUpFloor[currentFloor][j].Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y) && (detaX < detaY))
                {
                    //i在j下右
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].X < StaticObject.baseCenterUpFloor[currentFloor][i].X)
                    {
                        //i垂直向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 0; }
                        else
                        { return 1; }
                    }
                    else//i在j下左
                    {
                        //i垂直向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 2; }
                        else
                        { return 1; }
                    }
                }
                //i在j左侧
                else if ((StaticObject.baseCenterUpFloor[currentFloor][j].X > StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j左下
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 1; }
                        else
                        { return 2; }
                    }
                    else//i在j左上
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 3; }
                        else
                        { return 2; }
                    }
                }
                //i在j上侧
                else
                {
                    //i在j上左
                    if (StaticObject.baseCenterUpFloor[currentFloor][j].X > StaticObject.baseCenterUpFloor[currentFloor][i].X)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 2; }
                        else
                        { return 3; }
                    }
                    else//i在j上右
                    {
                        //i水平向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 0; }
                        else
                        { return 3; }
                    }
                }
            }
            #endregion
        }
        //获取建筑重叠、或未满足建筑间距产生的斥力
        public Vector3d GetOverlapVector(int i, int j, int currentFloor)
        {
            double precise = 0.001;
            double detaX = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].X - StaticObject.baseCenterUpFloor[currentFloor][i].X);
            double distanceXMin = StaticObject.baseHalfXUpFloor[currentFloor][i] + StaticObject.baseHalfXUpFloor[currentFloor][j];
            double detaY = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].Y - StaticObject.baseCenterUpFloor[currentFloor][i].Y);
            double distanceYMin = StaticObject.baseHalfYUpFloor[currentFloor][i] + StaticObject.baseHalfYUpFloor[currentFloor][j];
            //X、Y轴移动方向
            double signX = (StaticObject.baseCenterUpFloor[currentFloor][i].X - StaticObject.baseCenterUpFloor[currentFloor][j].X) / Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].X - StaticObject.baseCenterUpFloor[currentFloor][j].X);
            double signY = (StaticObject.baseCenterUpFloor[currentFloor][i].Y - StaticObject.baseCenterUpFloor[currentFloor][j].Y) / Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][i].Y - StaticObject.baseCenterUpFloor[currentFloor][j].Y);
            //重叠时，求移动向量
            if ((detaX - distanceXMin < precise) && (detaY - distanceYMin < precise))
            {
                Vector3d distanceBetween;
                double sign;//移动方向
                //比较X/Y哪个方向移动小
                double moveX = distanceXMin - detaX;
                double moveY = distanceYMin - detaY;

                if ((detaX < precise) || (detaY < precise))//X/Y2场馆重合
                {
                    //确定移动方向
                    if (i > j)//场馆编号大，正向移动
                    {
                        sign = 1;
                    }
                    else
                    {
                        sign = -1;
                    }
                    if (Math.Abs(moveX) <= Math.Abs(moveY))//X轴方向移动更近
                    {
                        if ((Manager.pushCount > StaticObject.pushLimitCount * 2) && (Manager.pushCount <= StaticObject.pushLimitCount * 3))
                        {
                            if (detaY == 0)
                            {
                                distanceBetween = new Vector3d(0, moveY * sign, 0);
                            }
                            else
                            {
                                distanceBetween = new Vector3d(0, moveY * signY, 0);
                            }
                        }
                        else
                        {
                            if (detaX == 0)
                            {
                                distanceBetween = new Vector3d(moveX * sign, 0, 0);
                            }
                            else
                            {
                                distanceBetween = new Vector3d(moveX * signX, 0, 0);
                            }
                        }
                    }
                    else//Y轴方向移动更近
                    {
                        if ((Manager.pushCount > StaticObject.pushLimitCount * 2) && (Manager.pushCount <= StaticObject.pushLimitCount * 3))
                        {
                            if (detaX == 0)
                            {
                                distanceBetween = new Vector3d(moveX * sign, 0, 0);
                            }
                            else
                            {
                                distanceBetween = new Vector3d(moveX * signX, 0, 0);
                            }
                        }
                        else
                        {
                            if (detaY == 0)
                            {
                                distanceBetween = new Vector3d(0, moveY * sign, 0);
                            }
                            else
                            {
                                distanceBetween = new Vector3d(0, moveY * signY, 0);
                            }
                        }
                    }
                }
                else//X/Y2场馆不重合
                {
                    if ((Manager.pushCount > StaticObject.pushLimitCount * 2) && (Manager.pushCount <= StaticObject.pushLimitCount * 3))//超过极限次数，尝试转向
                    {
                        if (Math.Abs(moveX) <= Math.Abs(moveY))//X轴方向移动更近
                        {
                            distanceBetween = new Vector3d(0, moveY * signY, 0);
                        }
                        else//Y轴方向移动更近
                        {
                            distanceBetween = new Vector3d(moveX * signX, 0, 0);
                        }
                    }
                    else//未超过极限次数
                    {
                        if (Math.Abs(moveX) <= Math.Abs(moveY))//X轴方向移动更近
                        {
                            distanceBetween = new Vector3d(moveX * signX, 0, 0);
                        }
                        else//Y轴方向移动更近
                        {
                            distanceBetween = new Vector3d(0, moveY * signY, 0);
                        }
                    }
                }
                return distanceBetween;
            }
            else { return Vector3d.Zero; }
        }
        //是否完成边界力计算并拉回的环节
        public bool IfFinishBoundaryForce(int currentFloor, List<Vector3d> boundaryForce)
        {
            if (boundaryForce != null)
            {
                for (int i = 0; i < boundaryForce.Count; i++)
                {
                    if (boundaryForce[i].Length != 0)
                    {
                        return false;//若存在不为0的
                    }
                }
                return true;
            }
            else
            {
                return true;
            }
        }
        //针对单个场馆完成躲避障碍物，并保证躲避后不会出界
        public bool AvoidBarrier(int currentFloor, int item, List<Curve> barrier)
        {
            if ((currentFloor % 2 == 0) && (item == 0))//若是大厅，则直接跳过此步骤
            {
                return false;
            }

            #region 数据准备
            Plane plane = new Plane();//障碍物所在平面
            double maxError = 0.001;//可忽视的最小误差
            List<Vector3d> move = new List<Vector3d>();//避开障碍物所需要的力
            List<List<Vector3d>> moveOrigin = new List<List<Vector3d>>();//套用已有函数，包一层list，用于分辨障碍物力是否为0
            Vector3d moveTotal = Vector3d.Zero;//避开障碍物合力
            List<bool> forceStatus = new List<bool>();//用于记录、评估item是否不受障碍力。不受力=false
            #endregion

            #region 获取针对单一障碍物的受力
            for (int i = 0; i < barrier.Count; i++)
            {
                //求场馆与障碍物的的交点
                CurveIntersections intersect = Intersection.CurveCurve(StaticObject.baseBoundaryUpFloor[currentFloor][item], barrier[i], 0.1, 0.1);
                if (intersect.Count == 2)//若存在交点【注意！可能出问题】
                {
                    #region 数据准备
                    List<Point3d> vertices = GetVertices(currentFloor, StaticObject.baseBoundaryUpFloor[currentFloor][item]);//求待测曲线4个顶点
                    List<Point3d> vertices2 = GetVertices(currentFloor, barrier[i]);//求待场馆线4个顶点
                    List<Point3d> verticesInside = new List<Point3d>();//出界的场馆顶点
                    List<Point3d> verticesInside2 = new List<Point3d>();//出界的场馆顶点，障碍物、场馆反向
                    List<Point3d> crossPt = new List<Point3d>();//相交点
                    int insideCount = 0;//出界点
                    int insideCount2 = 0;//反向出界点
                    barrier[i].TryGetPlane(out plane);//获取边界平面
                    List<Vector3d> tempMove = new List<Vector3d>();//用于存储待比较移动向量
                    bool ifFlip = false;//若障碍物顶点在曲线范围内，为true
                    #endregion

                    //获取所有相交点
                    for (int k = 0; k < intersect.Count; k++)
                    {
                        crossPt.Add(intersect[k].PointA);
                    }

                    #region 检测是否有点出界
                    for (int j = 0; j < 4; j++)
                    {
                        PointContainment ptContain = barrier[i].Contains(vertices[j], plane, 0);
                        if (ptContain == PointContainment.Inside)//若场馆边界点出界了,累加出界次数
                        {
                            insideCount++;
                            verticesInside.Add(vertices[j]);//添加出界点
                        }
                    }
                    //待测曲线顶点未在障碍物内时，可能是障碍物顶点在曲线内
                    if (insideCount == 0)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            PointContainment ptContain = StaticObject.baseBoundaryUpFloor[currentFloor][item].Contains(vertices2[j], plane, 0);
                            if (ptContain == PointContainment.Inside)//若场馆边界点出界了,累加出界次数
                            {
                                insideCount2++;
                                verticesInside2.Add(vertices2[j]);//添加出界点
                                ifFlip = true;
                            }
                        }
                        if (insideCount2 == 0)//【注意！！！此处容易出错】
                        {
                            continue;
                        }
                    }
                    #endregion

                    #region 计算移出障碍物所需力
                    if (insideCount == 1)//1个角点在障碍物内
                    {
                        for (int m = 0; m < crossPt.Count; m++)
                        {
                            tempMove.Add(crossPt[m] - verticesInside[0]);
                        }
                        if (Manager.barrierCount <= StaticObject.barrierLimitCount)//未超过限定次数
                        {
                            //获取最短向量
                            move.Add(GetShortestVector(tempMove));
                        }
                        else//超过限定次数
                        {
                            //获取最长向量
                            move.Add(GetLongestVector(tempMove));
                        }
                        forceStatus.Add(true);
                    }
                    else if ((insideCount != 1) || (insideCount2 != 0))//2个角点在障碍物内，也可能出现4个点【注意！可能出问题】
                    {
                        if (ifFlip == false)//障碍物包含待测曲线顶点
                        {
                            for (int m = 0; m < crossPt.Count; m++)
                            {
                                for (int k = 0; k < insideCount; k++)
                                {
                                    if ((Math.Abs(crossPt[m].X - verticesInside[k].X) < maxError) || (Math.Abs(crossPt[m].Y - verticesInside[k].Y) < maxError))//交点与界内点垂直或平行
                                    {
                                        tempMove.Add(crossPt[m] - verticesInside[k]);
                                    }
                                }
                            }
                        }
                        else//待测曲线包含障碍物顶点
                        {
                            for (int m = 0; m < crossPt.Count; m++)
                            {
                                for (int k = 0; k < insideCount2; k++)
                                {
                                    if ((Math.Abs(crossPt[m].X - verticesInside2[k].X) < maxError) || (Math.Abs(crossPt[m].Y - verticesInside2[k].Y) < maxError))//交点与界内点垂直或平行
                                    {
                                        tempMove.Add(verticesInside2[k] - crossPt[m]);
                                    }
                                }
                            }
                        }
                        //获取最短向量
                        if (Manager.barrierCount <= StaticObject.barrierLimitCount)//未超过限定次数
                        {
                            //获取最长向量
                            move.Add(GetLongestVector(tempMove));
                        }
                        else//超过限定次数
                        {
                            //获取最短向量
                            move.Add(GetShortestVector(tempMove));
                        }
                        forceStatus.Add(true);
                    }
                    #endregion
                }
            }
            #endregion

            #region 综合单一场馆的所有障碍物受力
            if ((forceStatus == null) || (forceStatus.Count == 0))//不受障碍物力
            {
                return false;
            }
            else//受力
            {
                for (int i = 0; i < move.Count; i++)
                {
                    moveTotal += move[i];
                }
            }
            #endregion

            #region 移动出界检测
            Vector3d moveX = new Vector3d(moveTotal.X, 0, 0);
            Vector3d moveY = new Vector3d(0, moveTotal.Y, 0);
            double deltaX = IfMoveOverBoundary(currentFloor, StaticObject.baseBoundaryUpFloor[currentFloor][item], moveX).X;
            double deltaY = IfMoveOverBoundary(currentFloor, StaticObject.baseBoundaryUpFloor[currentFloor][item], moveY).Y;
            moveTotal = new Vector3d(deltaX, deltaY, 0);
            moveOrigin.Add(move);
            #endregion

            #region 本轮障碍力移动是否失败检测
            if (!IfSumEqualZero(moveOrigin))
            {
                Manager.barrierCount++;
            }
            #endregion

            #region 移动形体
            StaticObject.buildingPerFloor[currentFloor][item].Trans(Transform.Translation(moveTotal));
            StaticObject.baseCenterUpFloor[currentFloor][item] = StaticObject.buildingPerFloor[currentFloor][item].GetCenterPoint();
            #endregion

            return true;
        }
        //移动是否超出边界，针对障碍物力时的检测
        public Vector3d IfMoveOverBoundary(int currentFloor, Curve testOriginal, Vector3d move)
        {
            Curve testBoundary = GetCurrentLevelBoundary(currentFloor);//获取本层建筑退线
            Curve test = testOriginal.DuplicateCurve();
            test.Transform(Transform.Translation(move));//移动后的曲线位置
            CurveIntersections intersect = Intersection.CurveCurve(testBoundary, test, 0.1, 0.1);//是否出边
            List<Vector3d> testMove = new List<Vector3d>();//记录出边后各点的位移
            List<Point3d> vertices = GetVertices(currentFloor, test);//求待测曲线4个顶点
            List<Point3d> verticesOutside = new List<Point3d>();//记录出界点
            List<Point3d> crossPt = new List<Point3d>();//出边后对应到边界上的各平行最近点
            Vector3d finalMove = Vector3d.Zero;
            int outsideCount = 0;//出界点
            double maxError = 0.001;//可忽视的最小误差
            double x = test.GetBoundingBox(true).Max.X - test.GetBoundingBox(true).Min.X;//待测曲线X向长度
            double y = test.GetBoundingBox(true).Max.Y - test.GetBoundingBox(true).Min.Y;//待测曲线Y向长度

            Plane plane = new Plane();//边界所在平面
            testBoundary.TryGetPlane(out plane);//获取边界平面

            if (intersect.Count == 2)//如果交点=2
            {
                //添加交点
                for (int i = 0; i < intersect.Count; i++)
                {
                    crossPt.Add(intersect[i].PointA);
                }

                //检测是否有点出界
                for (int i = 0; i < 4; i++)
                {
                    PointContainment ptContain = testBoundary.Contains(vertices[i], plane, 0);
                    if (ptContain == PointContainment.Outside)//若出界了
                    {
                        outsideCount++;
                        verticesOutside.Add(vertices[i]);
                    }
                }

                //求解出界力
                if (outsideCount != 0)//若出界了
                {
                    if (verticesOutside.Count == 1)//出界1个点时，根据场馆方向确定选哪个方向的向量
                    {
                        if (move.X == 0)//原始移动向量moveY轴向
                        {
                            for (int i = 0; i < crossPt.Count; i++)
                            {
                                if (Math.Abs(crossPt[i].X - verticesOutside[0].X) < maxError)
                                {
                                    testMove.Add(crossPt[i] - verticesOutside[0]);//取与出界点水平的交点求向量
                                }
                            }
                        }
                        else//原始移动向量moveX轴向
                        {
                            for (int i = 0; i < crossPt.Count; i++)
                            {
                                if (Math.Abs(crossPt[i].Y - verticesOutside[0].Y) < maxError)
                                {
                                    testMove.Add(crossPt[i] - verticesOutside[0]);//取与出界点水平的交点求向量
                                }
                            }
                        }
                        Vector3d tempMove = GetLongestVector(testMove);//求2个平行向量中最长的
                        finalMove = move + tempMove;//求与输入Move的合力
                    }
                    else if (verticesOutside.Count == 2)
                    {
                        for (int i = 0; i < crossPt.Count; i++)
                        {
                            for (int j = 0; j < verticesOutside.Count; j++)
                            {
                                if ((Math.Abs(crossPt[i].Y - verticesOutside[j].Y) < maxError) || (Math.Abs(crossPt[i].X - verticesOutside[j].X) < maxError))
                                {
                                    testMove.Add(crossPt[i] - verticesOutside[j]);//取与出界点水平的交点求向量
                                }
                            }
                        }
                        Vector3d tempMove = GetLongestVector(testMove);//求2个平行向量中最长的
                        finalMove = move + tempMove;//求与输入Move的合力
                    }
                }

                //返回力
                return finalMove;
            }
            else if ((intersect.Count == 3) || (intersect.Count == 4))
            {
                return Vector3d.Zero;
            }
            else//未出界
            {
                return move;
            }
        }
        //是否合力为0,若完全不受力，则返回true
        public bool IfSumEqualZero(List<List<Vector3d>> input)
        {
            double maxError = 0.1;//可忽视的最小误差
            for (int i = 0; i < input.Count; i++)
            {
                foreach (Vector3d item in input[i])
                {
                    if (item.Length > maxError)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        //求合力
        public Vector3d GetSumVector(List<Vector3d> input)
        {
            Vector3d sum = Vector3d.Zero;
            foreach (var item in input)
            {
                sum += item;
            }
            return sum;
        }
        //场馆间受力后，是否与障碍物交叉
        public Vector3d IfIntersectWithBarrier(int currentFloor, int item, List<Curve> barrier, Vector3d move)
        {
            #region 数据准备
            Plane plane = new Plane();//障碍物所在平面
            double maxError = 0.001;//可忽视的最小误差
            Curve test = StaticObject.baseBoundaryUpFloor[currentFloor][item].DuplicateCurve();
            test.Transform(Transform.Translation(move));//获取移动后的边界
            List<Vector3d> moveTemp = new List<Vector3d>();//避开障碍物所需要的力
            Vector3d moveTotal = Vector3d.Zero;//避开障碍物合力
            #endregion

            for (int i = 0; i < barrier.Count; i++)
            {
                //求场馆与障碍物的的交点
                CurveIntersections intersect = Intersection.CurveCurve(test, barrier[i], 0.1, 0.1);
                if (intersect.Count == 2)//若存在交点【注意！可能出问题】
                {
                    #region 数据准备
                    List<Point3d> vertices = GetVertices(currentFloor, test);//求待测曲线4个顶点
                    List<Point3d> verticesInside = new List<Point3d>();//出界的场馆顶点
                    List<Point3d> crossPt = new List<Point3d>();//相交点
                    int insideCount = 0;//出界点
                    barrier[i].TryGetPlane(out plane);//获取边界平面
                    List<Vector3d> tempMove = new List<Vector3d>();//用于存储待比较移动向量
                    bool ifBarrierOverItem = false;//用于解决场馆
                    #endregion

                    //获取所有相交点
                    for (int k = 0; k < intersect.Count; k++)
                    {
                        crossPt.Add(intersect[k].PointA);
                    }

                    #region 检测是否有点出界
                    for (int j = 0; j < 4; j++)
                    {
                        PointContainment ptContain = barrier[i].Contains(vertices[j], plane, 0);
                        if (ptContain == PointContainment.Inside)//若场馆边界点出界了,累加出界次数
                        {
                            insideCount++;
                            verticesInside.Add(vertices[j]);//添加出界点
                        }
                        else if (ptContain == PointContainment.Coincident)//场馆移动后边界点与障碍物边界重合
                        {
                            Point3d barrierCenter = barrier[i].GetBoundingBox(true).Center;//障碍物中心
                            double distanceMinX = StaticObject.baseHalfXUpFloor[currentFloor][item] + 0.5 * (barrier[i].GetBoundingBox(true).Max.X - barrier[i].GetBoundingBox(true).Min.X);
                            double distanceMinY = StaticObject.baseHalfYUpFloor[currentFloor][item] + 0.5 * (barrier[i].GetBoundingBox(true).Max.Y - barrier[i].GetBoundingBox(true).Min.Y);
                            double deltaX = Math.Abs(test.GetBoundingBox(true).Center.X - barrierCenter.X);//场馆与障碍物X轴间距绝对值
                            double deltaY = Math.Abs(test.GetBoundingBox(true).Center.Y - barrierCenter.Y);//场馆与障碍物Y轴间距绝对值
                            if ((deltaX - distanceMinX <= maxError) && (deltaY - distanceMinY <= maxError))//场馆移动后会与障碍物相交，则场馆不移动
                            {
                                return Vector3d.Zero;
                            }
                        }
                    }
                    //正好边界重叠时，停止计算
                    if (insideCount == 0)
                    {
                        ifBarrierOverItem = true;
                        //检测是否是由于testCurve长于障碍物而无法求出内部点
                        vertices = GetVertices(currentFloor, barrier[i]);
                        for (int j = 0; j < 4; j++)
                        {
                            PointContainment ptContain = test.Contains(vertices[j], plane, 0);
                            if (ptContain == PointContainment.Inside)//若场馆边界点出界了,累加出界次数
                            {
                                insideCount++;
                                verticesInside.Add(vertices[j]);//添加出界点
                            }
                        }
                        if (insideCount == 0)
                        {
                            break;
                        }
                    }
                    #endregion

                    #region 计算移出障碍物所需力
                    if (insideCount == 1)//1个角点在障碍物内
                    {
                        if (ifBarrierOverItem == false)//场馆点在障碍物内
                        {
                            for (int m = 0; m < crossPt.Count; m++)
                            {
                                tempMove.Add(crossPt[m] - verticesInside[0]);
                            }
                        }
                        else//障碍物点在场馆内
                        {
                            for (int m = 0; m < crossPt.Count; m++)
                            {
                                tempMove.Add(verticesInside[0] - crossPt[m]);
                            }
                        }
                        //获取最短向量
                        moveTemp.Add(GetShortestVector(tempMove));
                    }
                    else//2个角点在障碍物内，也可能出现4个点【注意！可能出问题】
                    {
                        for (int m = 0; m < crossPt.Count; m++)
                        {
                            for (int k = 0; k < insideCount; k++)
                            {
                                if (ifBarrierOverItem == false)//场馆点在障碍物内
                                {
                                    if ((Math.Abs(crossPt[m].X - verticesInside[k].X) < maxError) || (Math.Abs(crossPt[m].Y - verticesInside[k].Y) < maxError))//交点与界内点垂直或平行
                                    {
                                        tempMove.Add(crossPt[m] - verticesInside[k]);
                                    }
                                }
                                else//障碍物点在场馆内
                                {
                                    if ((Math.Abs(crossPt[m].X - verticesInside[k].X) < maxError) || (Math.Abs(crossPt[m].Y - verticesInside[k].Y) < maxError))//交点与界内点垂直或平行
                                    {
                                        tempMove.Add(verticesInside[k] - crossPt[m]);
                                    }
                                }
                            }
                        }
                        //获取最短向量
                        moveTemp.Add(GetLongestVector(tempMove));
                    }
                    #endregion
                }
                else if (intersect.Count == 1)//与障碍物有整条边重叠，场馆照常移动
                {
                }
                else if ((intersect.Count == 3) || (intersect.Count == 4))//与障碍物有重叠时，场馆不移动
                {
                    return Vector3d.Zero;
                }
            }

            #region 综合单一场馆的所有障碍物受力
            for (int i = 0; i < moveTemp.Count; i++)
            {
                moveTotal += moveTemp[i];
            }
            #endregion

            //返回不与障碍物矛盾的移动力
            return (move + moveTotal);
        }
        //获取场馆与指定对象的位置关系，针对障碍物
        public int GetSpecificBuildingPosition(int currentFloor, int i, Curve testCurve)
        {
            #region 数据准备
            double testCurveX = testCurve.GetBoundingBox(true).Max.X - testCurve.GetBoundingBox(true).Min.X;//testCurve曲线X方向长度
            double testCurveY = testCurve.GetBoundingBox(true).Max.Y - testCurve.GetBoundingBox(true).Min.Y;//testCurve曲线Y方向长度
            Point3d testCenter = testCurve.GetBoundingBox(true).Center;//testCurve中心点
            double detaX = Math.Abs(testCenter.X - StaticObject.baseCenterUpFloor[currentFloor][i].X);//中心点间X长度
            double distanceXMin = testCurveX * 0.5 + StaticObject.baseHalfXUpFloor[currentFloor][i];
            double detaY = Math.Abs(testCenter.Y - StaticObject.baseCenterUpFloor[currentFloor][i].Y);//中心点间Y长度
            double distanceYMin = testCurveY * 0.5 + StaticObject.baseHalfYUpFloor[currentFloor][i];

            double precise = 0.001;//【此处经常出问题】允许的误差0.00000000000001
            double testX = detaX - distanceXMin;//误差检测
            double testY = detaY - distanceYMin;//误差检测
            #endregion

            #region 远离时
            if (((detaX - distanceXMin > -precise) || (detaY - distanceYMin > -precise)) && ((detaX - distanceXMin > -precise) && (detaY - distanceYMin > -precise)))
            {
                double moveX = Math.Abs(detaX - distanceXMin);
                double moveY = Math.Abs(detaY - distanceYMin);
                //i在j右上区域
                if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > testCenter.Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 4; }
                    else { return 11; }
                }
                //i在j正右上区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y >= testCenter.Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 0; }
                //i在j正右下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < testCenter.Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 0; }
                //i在j右下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X > testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < testCenter.Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 5; }
                    else { return 6; }
                }
                //i在j正下右区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X >= testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < testCenter.Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                { return 1; }
                //i在j正下左区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < testCenter.Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
                { return 1; }
                //i在j左下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y < testCenter.Y) && (detaY > distanceYMin) && (detaX > distanceXMin))
                {
                    if (moveX > moveY) { return 8; }
                    else { return 7; }
                }
                //i在j正左下区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y <= testCenter.Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 2; }
                //i在j正左上区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > testCenter.Y) && (detaY <= distanceYMin) && (detaX >= distanceXMin))
                { return 2; }
                //i在j左上区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X < testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > testCenter.Y) && (detaY >= distanceYMin) && (detaX >= distanceXMin))
                {
                    if (moveX > moveY) { return 9; }
                    else { return 10; }
                }
                //i在j正上左区域
                else if ((StaticObject.baseCenterUpFloor[currentFloor][i].X <= testCenter.X) && (StaticObject.baseCenterUpFloor[currentFloor][i].Y > testCenter.Y) && (detaY >= distanceYMin) && (detaX <= distanceXMin))
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
                if ((testCenter.X < StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j右上
                    if (testCenter.Y < StaticObject.baseCenterUpFloor[currentFloor][i].Y)
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
                else if ((testCenter.Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y) && (detaX < detaY))
                {
                    //i在j下右
                    if (testCenter.X < StaticObject.baseCenterUpFloor[currentFloor][i].X)
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
                else if ((testCenter.X > StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j左下
                    if (testCenter.Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y)
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
                    if (testCenter.X > StaticObject.baseCenterUpFloor[currentFloor][i].X)
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
                if ((testCenter.X < StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j右上
                    if (testCenter.Y < StaticObject.baseCenterUpFloor[currentFloor][i].Y)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 3; }
                        else
                        { return 0; }
                    }
                    else//i在j右下
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 1; }
                        else
                        { return 0; }
                    }
                }
                //i在j下侧
                else if ((testCenter.Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y) && (detaX < detaY))
                {
                    //i在j下右
                    if (testCenter.X < StaticObject.baseCenterUpFloor[currentFloor][i].X)
                    {
                        //i垂直向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 0; }
                        else
                        { return 1; }
                    }
                    else//i在j下左
                    {
                        //i垂直向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 2; }
                        else
                        { return 1; }
                    }
                }
                //i在j左侧
                else if ((testCenter.X > StaticObject.baseCenterUpFloor[currentFloor][i].X) && (detaX >= detaY))
                {
                    //i在j左下
                    if (testCenter.Y > StaticObject.baseCenterUpFloor[currentFloor][i].Y)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 1; }
                        else
                        { return 2; }
                    }
                    else//i在j左上
                    {
                        //i水平向长
                        if (StaticObject.baseHalfXUpFloor[currentFloor][i] > detaX * ratioPlus)
                        { return 3; }
                        else
                        { return 2; }
                    }
                }
                //i在j上侧
                else
                {
                    //i在j上左
                    if (testCenter.X > StaticObject.baseCenterUpFloor[currentFloor][i].X)
                    {
                        //i水平向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 2; }
                        else
                        { return 3; }
                    }
                    else//i在j上右
                    {
                        //i水平向长
                        if (StaticObject.baseHalfYUpFloor[currentFloor][i] > detaY * ratioPlus)
                        { return 0; }
                        else
                        { return 3; }
                    }
                }
            }
            #endregion
        }
        //获取场馆与指定对象的位置关系，针对场馆
        public int GetSpecificBuildingPosition(int currentFloor, int i, int j)
        {
            //数据准备
            double detaX = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].X - StaticObject.baseCenterUpFloor[currentFloor][i].X);//中心点间X长度
            double distanceXMin = StaticObject.baseHalfXUpFloor[currentFloor][j] + StaticObject.baseHalfXUpFloor[currentFloor][i];
            double detaY = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].Y - StaticObject.baseCenterUpFloor[currentFloor][i].Y);//中心点间Y长度
            double distanceYMin = StaticObject.baseHalfYUpFloor[currentFloor][j] + StaticObject.baseHalfYUpFloor[currentFloor][i];

            //求场馆与指定对象的位置
            return GetOtherBuildingPosition(detaX, distanceXMin, detaY, distanceYMin, currentFloor, i, j);
        }
        //是否比指定场馆距离大厅更近，返回true=场馆移动，返回false=场馆不移动
        public Vector3d ifIsTheNearestBuilding(int currentFloor, int i, double itemX, double itemY, int lobbyPosition, bool ifIntersect)//i=场馆索引号
        {
            double precise = 0.001;//允许的最大误差

            //4层及以上不移动【项目局限性】
            if (currentFloor > 3)
            {
                return Vector3d.Zero;//i不用移动了 
            }

            #region 偶数楼层
            if (currentFloor % 2 == 0)
            {
                for (int j = 1; j < StaticObject.buildingsUpFloor[currentFloor].Count; j++)
                {
                    if (j != i)//不是场馆自身
                    {
                        //数据准备
                        double detaX = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].X - StaticObject.baseCenterUpFloor[currentFloor][0].X);//中心点间X长度
                        double distanceXMin = StaticObject.baseHalfXUpFloor[currentFloor][j] + StaticObject.baseHalfXUpFloor[currentFloor][0];
                        double detaY = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].Y - StaticObject.baseCenterUpFloor[currentFloor][0].Y);//中心点间Y长度
                        double distanceYMin = StaticObject.baseHalfYUpFloor[currentFloor][j] + StaticObject.baseHalfYUpFloor[currentFloor][0];

                        //求场馆与指定对象的位置
                        if (GetOtherBuildingPosition(detaX, distanceXMin, detaY, distanceYMin, currentFloor, j, 0) == lobbyPosition)//若与大厅所在方向相同
                        {
                            if ((lobbyPosition == 0) || (lobbyPosition == 2))//场馆i位于大厅的右侧或左侧
                            {
                                if (((detaX - distanceXMin) < itemX) && ((detaX - distanceXMin) > -precise))//场馆j到大厅的距离更近,且不贴临大厅
                                {
                                    double detaYBetweenIAndJ = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].Y - StaticObject.baseCenterUpFloor[currentFloor][i].Y);//中心点间Y长度
                                    double distanceYMinBetweenIAndJ = StaticObject.baseHalfYUpFloor[currentFloor][j] + StaticObject.baseHalfYUpFloor[currentFloor][i];

                                    if (detaYBetweenIAndJ - distanceYMinBetweenIAndJ < 0)//j与i水平移动相互干扰
                                    {
                                        return Vector3d.Zero;//i不用移动了
                                    }
                                }
                            }
                            else if ((lobbyPosition == 1) || (lobbyPosition == 3))//场馆i位于大厅的下侧或上侧
                            {
                                if (((detaY - distanceYMin) < itemY) && ((detaY - distanceYMin) > -precise))//场馆j到大厅的距离更近
                                {
                                    double detaXetweenIAndJ = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].X - StaticObject.baseCenterUpFloor[currentFloor][i].X);//中心点间Y长度
                                    double distanceXMinBetweenIAndJ = StaticObject.baseHalfXUpFloor[currentFloor][j] + StaticObject.baseHalfXUpFloor[currentFloor][i];

                                    if (detaXetweenIAndJ - distanceXMinBetweenIAndJ < 0)//j与i水平移动相互干扰
                                    {
                                        return Vector3d.Zero;//i不用移动了
                                    }
                                }
                            }
                        }
                        else//处理j与大厅相交，j的相对位置有2种但没取lobbyPosition的情况
                        {
                            //求场馆i移动后的位置
                            Curve itemMove = StaticObject.baseBoundaryUpFloor[currentFloor][i].DuplicateCurve();
                            itemMove.Transform(Transform.Translation(GetOrientationVector(itemX, itemY, 0, lobbyPosition)));
                            //求场馆i移动后与场馆j是否相交
                            CurveIntersections intersect = Intersection.CurveCurve(itemMove, StaticObject.baseBoundaryUpFloor[currentFloor][j], 0.1, 0.1);
                            if (intersect.Count > 1)
                            {
                                return Vector3d.Zero;//i不用移动了
                            }
                        }
                    }
                }
            }
            #endregion

            #region 奇数楼层
            else
            {
                for (int j = 0; j < StaticObject.buildingsUpFloor[currentFloor].Count; j++)
                {
                    if (j != i)//不是场馆自身
                    {
                        //数据准备
                        double detaX = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].X - StaticObject.barrier[currentFloor][0].GetBoundingBox(true).Center.X);//中心点间X长度
                        double barrierLobbyHalfX = (StaticObject.barrier[currentFloor][0].GetBoundingBox(true).Max.X - StaticObject.barrier[currentFloor][0].GetBoundingBox(true).Min.X);//大厅作为障碍物的X半长度
                        double distanceXMin = StaticObject.baseHalfXUpFloor[currentFloor][j] + barrierLobbyHalfX;
                        double detaY = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].Y - StaticObject.barrier[currentFloor][0].GetBoundingBox(true).Center.Y);//中心点间Y长度
                        double barrierLobbyHalfY = (StaticObject.barrier[currentFloor][0].GetBoundingBox(true).Max.Y - StaticObject.barrier[currentFloor][0].GetBoundingBox(true).Min.Y);//大厅作为障碍物的X半长度
                        double distanceYMin = StaticObject.baseHalfYUpFloor[currentFloor][j] + barrierLobbyHalfY;

                        //求场馆与指定对象的位置
                        if (GetSpecificBuildingPosition(currentFloor, j, StaticObject.barrier[currentFloor][0]) == lobbyPosition)//若与大厅所在方向相同
                        {
                            if ((lobbyPosition == 0) || (lobbyPosition == 2))//场馆i位于大厅的右侧或左侧
                            {
                                if (((detaX - distanceXMin) < itemX) && ((detaX - distanceXMin) > -precise))//场馆j到大厅的距离更近,且不贴临大厅
                                {
                                    double detaYBetweenIAndJ = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].Y - StaticObject.baseCenterUpFloor[currentFloor][i].Y);//中心点间Y长度
                                    double distanceYMinBetweenIAndJ = StaticObject.baseHalfYUpFloor[currentFloor][j] + StaticObject.baseHalfYUpFloor[currentFloor][i];

                                    if (detaYBetweenIAndJ - distanceYMinBetweenIAndJ < -precise)//j与i水平移动相互干扰
                                    {
                                        return Vector3d.Zero;//i不用移动了
                                    }
                                }
                            }
                            else//场馆i位于大厅的下侧或上侧
                            {
                                if (((detaY - distanceYMin) < itemY) && ((detaY - distanceYMin) > -precise))//场馆j到大厅的距离更近
                                {
                                    double detaXBetweenIAndJ = Math.Abs(StaticObject.baseCenterUpFloor[currentFloor][j].X - StaticObject.baseCenterUpFloor[currentFloor][i].X);//中心点间Y长度
                                    double distanceXMinBetweenIAndJ = StaticObject.baseHalfXUpFloor[currentFloor][j] + StaticObject.baseHalfXUpFloor[currentFloor][i];

                                    if (detaXBetweenIAndJ - distanceXMinBetweenIAndJ < -precise)//j与i水平移动相互干扰
                                    {
                                        return Vector3d.Zero;//i不用移动了
                                    }
                                }
                            }
                        }
                        else//处理j与大厅相交，j的相对位置有2种但没取lobbyPosition的情况
                        {
                            //求场馆i移动后的位置
                            Curve itemMove = StaticObject.baseBoundaryUpFloor[currentFloor][i].DuplicateCurve();
                            itemMove.Transform(Transform.Translation(GetOrientationVector(itemX, itemY, 0, lobbyPosition)));
                            //求场馆i移动后与场馆j是否相交
                            CurveIntersections intersect = Intersection.CurveCurve(itemMove, StaticObject.baseBoundaryUpFloor[currentFloor][j], 0.1, 0.1);
                            if (intersect.Count > 1)
                            {
                                return Vector3d.Zero;//i不用移动了
                            }
                        }
                    }
                }
            }
            #endregion

            #region 出界及障碍物检测
            //若lobbyOrientation的方向没有其他干扰，场馆将移动的向量
            if (ifIntersect == false)//若场馆没有与大厅相交
            {
                Vector3d move = GetOrientationVector(itemX, itemY, 0, lobbyPosition);
                //是否出界判断
                Vector3d tempMove = IfMoveOverBoundary(currentFloor, StaticObject.baseBoundaryUpFloor[currentFloor][i], move);
                //是否与障碍物矛盾【可能出问题】
                if (tempMove.Length != 0)//考虑出界问题后仍可以移动
                {
                    //计算考虑障碍物后移动距离
                    Vector3d barrierMove = IfIntersectWithBarrier(currentFloor, i, StaticObject.barrier[currentFloor], tempMove);
                    if (tempMove != barrierMove)//若受到其他障碍物的影响
                    {
                        return Vector3d.Zero;//i不用移动了
                    }
                    else
                    {
                        return barrierMove;
                    }
                }
                else//受出界影响，i不移动了
                {
                    return tempMove;
                }
            }
            else//若若场馆与大厅相交
            {
                return Vector3d.Zero;//i不用移动了
            }
            #endregion
        }
        //给相对位置、间距，求移动向量
        public Vector3d GetOrientationVector(double deltaX, double deltaY, double offset, int orientation)//deltaX/deltaY是两场馆间净间距，orientation=i相对j的移动方向，offset=两场馆最小间距
        {
            switch (orientation)
            {
                //i在j右
                case 0:
                    return new Vector3d(-(deltaX - offset), 0, 0);
                //i在j下
                case 1:
                    return new Vector3d(0, deltaY - offset, 0);
                //i在j左
                case 2:
                    return new Vector3d(deltaX - offset, 0, 0);
                //i在j上
                case 3:
                    return new Vector3d(0, -(deltaY - offset), 0);
                default:
                    return Vector3d.Zero;
            }
        }
        //两曲线是否相交
        public bool IfIntersect(Curve curve1, Curve curve2)
        {
            CurveIntersections intersect = Intersection.CurveCurve(curve1, curve2, 0.1, 0.1);
            if (intersect.Count > 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        //大厅吸引移动时，若超长，移动后是否超出下部场馆范围能允许的悬挑
        public Vector3d IfCanStillPull(int currentFloor, int i, Curve[] baseBoundary, List<int> groundBoundaryIndex, Vector3d move)
        {
            //获取场馆i移动后的曲线信息
            Curve item = StaticObject.baseBoundaryUpFloor[currentFloor][i].DuplicateCurve();
            item.Transform(Transform.Translation(move));
            //获取下部场馆曲线信息
            Curve itemUnder = baseBoundary[groundBoundaryIndex[i]].DuplicateCurve();

            if ((move.X != 0) && (move.Y == 0))//向X轴移动
            {
                if (move.X > 0)//向右移动
                {
                    if (item.GetBoundingBox(true).Max.X - StaticObject.offset < itemUnder.GetBoundingBox(true).Max.X)//没有移出下部场馆边界太多
                    {
                        return move;
                    }
                    else//超出下部场馆太多，不移动了
                    {
                        return Vector3d.Zero;
                    }
                }
                else//向左移动
                {
                    if (item.GetBoundingBox(true).Min.X + StaticObject.offset > itemUnder.GetBoundingBox(true).Min.X)//没有移出下部场馆边界太多
                    {
                        return move;
                    }
                    else//超出下部场馆太多，不移动了
                    {
                        return Vector3d.Zero;
                    }
                }
            }
            else if ((move.X == 0) && (move.Y != 0))//向Y轴移动
            {
                if (move.Y > 0)//向上移动
                {
                    if (item.GetBoundingBox(true).Max.Y - StaticObject.offset < itemUnder.GetBoundingBox(true).Max.Y)//没有移出下部场馆边界太多
                    {
                        return move;
                    }
                    else//超出下部场馆太多，不移动了
                    {
                        return Vector3d.Zero;
                    }
                }
                else//向下移动
                {
                    if (item.GetBoundingBox(true).Min.Y + StaticObject.offset > itemUnder.GetBoundingBox(true).Min.Y)//没有移出下部场馆边界太多
                    {
                        return move;
                    }
                    else//超出下部场馆太多，不移动了
                    {
                        return Vector3d.Zero;
                    }
                }
            }
            else//move的x/y均不为0【！！！占位，有问题了再扩展】
            {
                return Vector3d.Zero;
            }
        }
        //重绘显示网格
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (Manager.ifBegin2F)
            {
                DisplayMaterial material;
                for (int i = 1; i < StaticObject.buildingPerFloor.Count; i++)
                {
                    if (StaticObject.buildingPerFloor[i] != null)
                    {
                        for (int j = 0; j < StaticObject.buildingPerFloor[i].Count; j++)
                        {
                            if (StaticObject.buildingPerFloor[i][j] != null)
                            {
                                switch (Tool.GetBuildingType(StaticObject.buildingPerFloor[i][j]))
                                {
                                    case BuildingType.篮球比赛馆:
                                        material = new DisplayMaterial(System.Drawing.Color.Purple);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.篮球训练馆:
                                        material = new DisplayMaterial(System.Drawing.Color.Orchid);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.游泳馆:
                                        material = new DisplayMaterial(System.Drawing.Color.PowderBlue);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.羽毛球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.GreenYellow);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.网球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.LimeGreen);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.冰球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.LightSkyBlue);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.乒乓球馆:
                                        material = new DisplayMaterial(System.Drawing.Color.Yellow);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.健身馆:
                                        material = new DisplayMaterial(System.Drawing.Color.Pink);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.办公:
                                        material = new DisplayMaterial(System.Drawing.Color.Brown);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.观演厅:
                                        material = new DisplayMaterial(System.Drawing.Color.Purple);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.大厅:
                                        material = new DisplayMaterial(System.Drawing.Color.White);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    case BuildingType.其他:
                                        material = new DisplayMaterial(System.Drawing.Color.Gray);
                                        args.Display.DrawBrepShaded(Brep.CreateFromBox(StaticObject.buildingsUpFloor[i][j].Value), material);
                                        break;
                                    default:
                                        break;
                                }

                            }
                        }
                    }
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources._11_上层移动;

        public override Guid ComponentGuid
        {
            get { return new Guid("E37EBEF8-AFF9-4711-9F94-14215DA23B67"); }
        }
    }
}