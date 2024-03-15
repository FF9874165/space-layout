using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Space_Layout
{
    public class SwimmingPool : Court
    {
        public int laneNumber;
        public double rotation = 0;
        public double height = 12;
        public GH_Rectangle swimmingPool;

        //构造函数
        public SwimmingPool(CourtType courtType, int laneNumber)
        {
            this.courtType = courtType;

            switch (courtType)
            {
                #region"标准游泳池" 训练级别
                case CourtType.标准游泳池:
                    lengthPerCourt = 51.6;
                    this.laneNumber = laneNumber;
                    widthPerCourt = 2.5 * laneNumber + 0.16;
                    clearHeight = 2 + 8;//2米池底深

                    terminalDistance = 5;
                    sidelineDistance = 2;
                    terminalSpacing = 10;
                    sidelineSpacing = 4;
                    break;
                #endregion

                #region"非标游泳池"
                case CourtType.非标游泳池:
                    lengthPerCourt = 26.6;
                    this.laneNumber = laneNumber;
                    widthPerCourt = 2.5 * laneNumber + 0.16;
                    clearHeight = 2 + 8;//2米池底深

                    terminalDistance = 5;
                    sidelineDistance = 2;
                    terminalSpacing = 10;
                    sidelineSpacing = 4;
                    break;
                #endregion

                #region"儿童戏水池"
                case CourtType.儿童戏水池:
                    lengthPerCourt = 15;
                    widthPerCourt = 10;
                    clearHeight = 8 + 1;//1米池底深

                    terminalDistance = 2;//预估
                    sidelineDistance = 2;//预估
                    terminalSpacing = 4;//预估
                    sidelineSpacing = 4;//预估
                    break;
                    #endregion
            }

        }

        //绘制泳池实体
        public void DrawSwimmingPool()
        {
            //创建游泳池边界
            swimmingPool = new GH_Rectangle(new Rectangle3d(Plane.WorldXY, widthPerCourt, lengthPerCourt));
        }
    }
}
