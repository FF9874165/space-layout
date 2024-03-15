using NPOI.SS.Formula.Functions;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //用于控制重载计算的委托
    public delegate void RestartEventHandler();

    //总控空间布局的全流程
    public static class Manager
    {
        //控制相关组件重启的event
        public static event RestartEventHandler restartEvent;
        
        //空间布局的次数
        public static int runningTime = 0;
        //空间布局成功生成的次数
        public static int successTime = 0;
        //当前空间布局楼层
        public static int currentFloor = 0;
        //控制键：开始新一轮布局生成
        public static bool restart = false;
        //控制键：开启GH绘制
        public static bool ifBegin1FDrawing = false;

        //本轮首层布局推力计算次数
        public static int calculationTimes = 0;
        public static bool ifBeginCalculate = true;
        //本轮首层布局大厅吸引计算次数
        public static int pullTimes = 0;
        public static bool ifPullCalculate = false;//大厅吸引阶段的开关
        public static bool ifStop = false;//控制pullTimes的第二道闸门
        //统计移动量，用于解决首层推力散不开的问题
        public static List<Vector3d> moveSum = new List<Vector3d>();
        //首层布局场馆收缩
        public static bool ifShrinkPrepareOK = false;//首层布局场馆收缩工作开关
        public static int shrinkIndex = 1;//shrinkOrder
        public static int shrinkCount = 0;//收缩运算第几轮
        public static bool ifShrinkOk = false;//收缩是否完成
        //二层布局开始
        public static bool ifBegin2F = false;//是否开始二层布局
        public static List<bool> ifUpFloorInitializeOk = new List<bool>();//是否二层及以上布局完成首次摆放
        public static List<bool> ifUpFloorBoundaryForceOK = new List<bool>();//是否完成边界力计算，并确保场馆在界内
        public static List<bool> ifUpFloorBarrierForceOK = new List<bool>();//是否完成障碍物力计算，确保场馆在放入边界后避开障碍物
        public static List<bool> ifUpFloorPushForceOK = new List<bool>();//是否完成场馆间推力计算，确保场馆间不重叠、不出界、不与障碍物重叠
        public static List<bool> ifUpFloorPullForceOK = new List<bool>();//是否完成场馆间拉力计算
        //二层及以上边界力计算
        public static int boundaryCount = 0;//边界力运算第几轮，到达限值时要强制移入界内
        public static int barrierCount = 0;//场馆间障碍物力运算第几轮，到达限值时本轮失败
        public static int pushCount = 0;//场馆间推力运算第几轮，到达限值时本轮失败
        public static bool ifPushModeChange = false;//当为false时，计入Lobby的力进行移动，若为true时，不计Lobby的力进行移动
        //二层及以上布局结束
        public static List<bool> ifUpFloorLayoutBegin = new List<bool>();//二层布局完成后变为false
        public static bool ifFinishLayout = false;//二层布局完成后变为true
        public static int upFloorCount = 0;//二层移动计算限值

        //评价环节
        public static bool ifEvaluateOK = false;//评价环节准备工作是否完成
        public static bool ifAvoidCalculateTwice=false;//解决不明原因的重新计算后的二次闪算问题，successTime加2次

        //重启下一轮计算
        public static void RestartManager()
        {
            Manager.ifEvaluateOK = false;
            Manager.restart = false;
            ifFinishLayout=false;
            ifAvoidCalculateTwice = true;
            restartEvent();

        }
    }
}
