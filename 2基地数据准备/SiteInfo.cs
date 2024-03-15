using Rhino.Geometry;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Space_Layout
{
    //此项目存储基地任务书信息的静态类
    public static class SiteInfo
    {
        public static GH_Curve siteBoundary;//建设用地红线
        public static GH_Curve siteRetreat;//建筑控制线
        public static bool alreadyGetInfo = false;//【【要清理】】用于控制每个项目只获取一次基地信息

        public static double siteArea;//建设用地面积
        public static double buildingDensity = 0.4;//建筑密度
        public static double largestGroundFloorArea;//首层最大建筑面积理论值
        public static double plotRatio = 1.0;//容积率
        public static double largestBuildingArea;//地上总建筑面积最大值
        public static double siteHeight = 24;//最大建筑高度
        public static Orientation siteOrientation;//建设用地朝向

        //实际情况
        public static double buildingDensityActual = 0;//布局完成后，实际的容积率
        public static double groundFloorAreaActual = 0;//布局完成后，实际的首层面积

        //获取基地轮廓后，补充信息，每个项目仅进行一次
        public static void GetInfo(GH_Curve input, double buildingDensity1, double plotRatio1, double siteHeight1, double maxArea)
        {
            buildingDensity = buildingDensity1;
            plotRatio = plotRatio1;
            siteHeight = siteHeight1;
            siteBoundary = input;

            #region 计算面积
            //计算首层最大建筑面积（建设用地面积×建筑密度）
            AreaMassProperties compute = AreaMassProperties.Compute(input.Value);
            siteArea = compute.Area;
            largestGroundFloorArea = siteArea * buildingDensity;

            //计算地上总建筑面积最大值
            if (maxArea > 0)//若输入值有效，采用输入值
            {
                largestBuildingArea = maxArea;
            }
            else//采用理论值
            {
                largestBuildingArea = siteArea * plotRatio;
            }
            #endregion

            #region 获取朝向
            double xSize = input.Boundingbox.Max.X - input.Boundingbox.Min.X;
            double ySize = input.Boundingbox.Max.Y - input.Boundingbox.Min.Y;
            if (xSize < ySize)//垂直的
            {
                siteOrientation = Orientation.垂直;
            }
            else //水平的
            {
                siteOrientation = Orientation.水平;
            }
            #endregion

            //信息填写是否完毕
            alreadyGetInfo = true;
        }
    }
}
