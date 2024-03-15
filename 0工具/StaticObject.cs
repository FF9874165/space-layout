using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    public static class StaticObject
    {
        //每一轮平面布局时的各建筑场馆类
        public static BasketballMatchBuilding basketballMatchBuilding = null;//篮球比赛馆
        public static GeneralCourtBuilding basketballTrainingBuilding = null;//篮球训练馆
        public static AquaticBuilding aquaticBuilding = null;//游泳馆
        public static GeneralCourtBuilding badmintonBuilding = null;//羽毛球馆
        public static GeneralCourtBuilding tennisBuilding = null;//网球馆
        public static GeneralCourtBuilding iceHockeyBuilding = null;//冰球馆
        public static GeneralCourtBuilding tableTennisBuilding = null;//乒乓球馆
        public static Gymnasium gymnasium = null;//健身馆
        public static Office office = null;//办公
        public static Theater theater = null; //观演厅
        public static Lobby lobby = null;//大厅
        public static OtherFunction otherFunction = null; // 其他

        public static double offset = 8;//布局允许扩展距离
        public static double baseOffset = 6;//防火间距
        public static double standardFloorHeight = 6;//标准楼层高度
        public static double accuracy2 = 0.01;//误差辨别最小值
        public static double accuracy3 = 0.001;//误差辨别最小值

        //平面布局列表
        public static List<ITrans> buildingToBeArranged = null;
        public static List<ITrans> lobbyToBeArranged = null;
        public static List<List<ITrans>> buildingPerFloor = null;//各层待布置建筑列表
        public static List<Curve[]> floorBoundary = null;//边界线
        public static List<Curve[]> floorBoundaryUnion = null;//边界线同层合并后
        public static List<double> areaPerFloorActual = null;

        //首层数据传递
        public static bool ifGroundLobbyFindLocation = false;
        public static int floorCount = 0;
        public static double[] boundaryValue = new double[4];
        public static List<BuildingType> buildingTypes = null;
        public static List<GH_Box> buildings = null;
        public static List<GH_Box> auxiliary = null;
        public static List<Curve> baseBoundary = null;//单体首层边界线
        public static List<Curve> ceilingBoundary = null;//单体顶层边界线
        public static List<GH_Rectangle> courtR = null;//运动场边界
        public static List<Point3d> baseCenter = null;//底面中心点，用于计算移动向量
        public static List<double> baseHalfX = null;//底面一半X轴向尺寸，用于计算移动向量
        public static List<double> baseHalfY = null;//底面一半Y轴向尺寸，用于计算移动向量
        public static List<int> shrinkOrder = null;//首层布局收缩的顺序
        public static List<bool> shrinkStatus = null;//首层布局收缩完成情况
        public static int pullTimeChange = 500;//pulltime容忍BUG的计算次数

        //二层及以上楼层
        public static List<List<double>> AvailableArea = null;//设置初始位置时对应的下层面积列表
        public static List<List<BuildingType>> buildingTypesUpFloor = null;//二层及以上的建筑类型
        public static List<List<GH_Box>> buildingsUpFloor = null;//二层及以上的场馆轮廓
        public static List<List<GH_Box>> auxiliaryUpFloor = null;//二层及以上的辅助用房
        public static List<List<Curve>> baseBoundaryUpFloor = null;//二层及以上的单体边界线
        public static List<List<Curve>> ceilingBoundaryUpFloor = null;//二层及以上的单体顶层边界线
        public static List<List<GH_Rectangle>> courtUpFloor = null;//运动场边界
        public static List<List<Point3d>> baseCenterUpFloor = null;//二层及以上的底面中心点，用于计算移动向量
        public static List<List<double>> baseHalfXUpFloor = null;//二层及以上的底面一半X轴向尺寸，用于计算移动向量
        public static List<List<double>> baseHalfYUpFloor = null;//二层及以上的底面一半Y轴向尺寸，用于计算移动向量
        public static List<List<Curve>> barrier = null;//二层及以上该层的障碍物，不包括大厅
        public static List<List<int>> groundBoundaryIndex = null;//记录二层及以上场馆脚下踩着的是哪个场馆
        public static int pushLimitCount = 10;//场馆间计算次数上限值
        public static int barrierLimitCount = 2;//避开障碍物力计算次数上限值

        //数据统计
        public static List<List<double>> areaRequired = null;//各场馆任务书要求的面积
        public static List<List<double>> areaActual = null;//各场馆布局完成后实际的面积
        public static List<List<double>> areaDelta = null;//各场馆布局完成后,areaRequired-areaActual;

        public static double areaRequiredTotal;//各场馆任务书要求的面积之和
        public static double areaDeltaTotal;//各场馆面积差之和
        public static double areaFluctuationRate;//面积浮动率：areaDeltaTotal（绝对值）/areaRequiredTotal

        public static Curve[] compactnessCurve;//完成布局后，首层所有场馆的外放轮廓线的外接矩形，用以衡量布局的紧凑程度
        public static double compactness;//compactnessCurve对应的边长总和除以曲线总面积
        public static double lengthDivideArea;//首层轮廓线边长/面积的比值，描述布局的紧密程度
        public static double boundaryLength;//首层轮廓线边长总和，包括悬挑距离内场馆边界尽可能联通
        public static double boundaryArea;//首层轮廓线面积总和，包括悬挑距离内场馆边界尽可能联通

        public static List<List<bool>> ifConnectLobby = null;//各场馆与大厅的连接情况，包括在悬挑距离范围内的也记为可以联通
        public static double connectLobbyCount ;//与大厅可以联通的场馆数量，包括在悬挑距离范围内的也记为可以联通
        public static double buildingCount ;//总场馆数量，不含大厅
        public static double connectionRate ;//connectLobbyCount除以buildingCount

        public static bool ifEnergyConservation;//判断游泳馆与滑冰馆是否相邻，场馆间间距不大于StaticObject.offset时视为相邻

        public static bool ifSplitBuilding;//同类功能是否存在拆分现象
        public static double sameFunctionAdjoinRate;//相邻功能的场馆/存在拆分现象场馆数量之和（不计共享大厅）

        public static double areaOverLimitation;//超出悬挑边界对应的悬挑面积
        public static double areaOverLimitationRate;//超出悬挑边界对应的悬挑面积之和/任务书要求的总面积

        public static Brep[] unionResult;//所有场馆的并集
        public static double volume;//所有场馆体量求union
        public static double superficialArea;//所有场馆体量求union
        public static double shapeFactor;//体型系数

        //下一轮生成
        public static int timeSpan = 0;

        //结果布局展示
        public static int countPerRow = 4;//每行展示多少个方案
    }
}
