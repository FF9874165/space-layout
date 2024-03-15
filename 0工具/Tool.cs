using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //用于存放需随时调用的方法
    public static class Tool
    {
        //Random放外边，否则放每个函数里会得不到随机效果
        public static Random random = new Random(6);
        //获取随机布尔值
        public static bool GetBool()
        {
            if (random.Next(0, 2) == 0)
            {
                return false;
            }
            else { return true; }
        }
        //获取场馆切分为两个的几率
        public static double GetSplitRatio()
        {
            //系数控制在0.5或0.6
            return random.Next(5, 7) * 0.1;
        }
        //获取0-0.9间随机小数
        public static double GetRatio()
        {
            return random.Next(0, 10) * 0.1;
        }
        //获取指定范围内的小数
        public static double GetSpecificDouble(int start, int end)
        {
            return random.Next(start, end) * 0.1;
        }
        //若随机数小于ratio，逆时针转动90°，并记录旋转角度
        public static void IFRotateHalfPie(ref double rotation, double ratio)
        {
            if (Tool.GetRatio() < ratio) rotation = 0;
            else rotation = Math.PI * 90 / 180;
        }
        //若随机数小于ratio，逆时针转动90°
        public static double IFRotateHalfPie(double ratio)
        {
            if (Tool.GetRatio() < ratio)
            {
                return Math.PI * 90 / 180;
            }
            else return 0;
        }
        //角度弧度转换
        public static double AngleToRadians(double angle)
        {
            return angle * Math.PI / 180;
        }
        //通过ITrans接口获取建筑类型
        public static BuildingType GetBuildingType(ITrans toBeLayout)
        {
            if (toBeLayout is BasketballMatchBuilding)
            {
                return (toBeLayout as BasketballMatchBuilding).buildingType;
            }
            else if (toBeLayout is GeneralCourtBuildingGroup)
            {
                return (toBeLayout as GeneralCourtBuildingGroup).buildingType;
            }
            else if (toBeLayout is AquaticBuildingGroup)
            {
                return (toBeLayout as AquaticBuildingGroup).buildingType;
            }
            else if (toBeLayout is GymnasiumGroup)
            {
                return (toBeLayout as GymnasiumGroup).buildingType;
            }
            else if (toBeLayout is Office)
            {
                return (toBeLayout as Office).buildingType;
            }
            else if (toBeLayout is Theater)
            {
                return (toBeLayout as Theater).buildingType;
            }
            else if (toBeLayout is LobbyUnit)
            {
                return BuildingType.大厅;
            }
            else
            {
                return (toBeLayout as OtherFunction).buildingType;
            }
        }
    }
}
