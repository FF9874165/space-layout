using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //通用类球馆运动场地组
    public class GeneralCourtGroup : BallCourtGroup
    {
        //构造函数
        public GeneralCourtGroup(GeneralCourtBuilding generalCourtBuilding, int groupNumber)
        {
            this.generalCourtBuilding = generalCourtBuilding;
            this.groupNumber = groupNumber;
        }

        //确定场地排列数、总尺寸
        public override void BallCourtLayout(BallCourt ballCourt)
        {
            //计算这个球场组内的场地数量
            courtNumber = ballCourt.courtNumberPerGroup[groupNumber];

            //确定球场排布行列数
            GetBallCourtLayout(ballCourt);

            //球场平面排布
            if (rotation == 0)
            {
                courtTotalWidth = ballCourt.sidelineDistance * 2 + ballCourt.sidelineSpacing * (column - 1) + ballCourt.widthPerCourt * column;
                courtTotalLength = ballCourt.terminalDistance * 2 + ballCourt.terminalSpacing * (row - 1) + ballCourt.lengthPerCourt * row;
            }
            else//旋转90°排布
            {
                courtTotalWidth = ballCourt.terminalDistance * 2 + ballCourt.terminalSpacing * (column - 1) + ballCourt.lengthPerCourt * column;
                courtTotalLength = ballCourt.sidelineDistance * 2 + ballCourt.sidelineSpacing * (row - 1) + ballCourt.widthPerCourt * row;
            }
        }

        //求得boundary、courtOutline几何形态
        public override void DrawCourt(BallCourt ballCourt, bool multiFunctional)
        {
            #region 绘制运动场地轮廓
            //计算层高
            if (multiFunctional == false)
            {
                if (courtTotalWidth <= courtTotalLength)
                {
                    floorHeight = Math.Ceiling(ballCourt.clearHeight + courtTotalWidth / 12 + 0.8);//净高+梁高+吊顶高度
                }
                else
                {
                    floorHeight = Math.Ceiling(ballCourt.clearHeight + courtTotalLength / 12 + 0.8);//净高+梁高+吊顶高度
                }
            }
            else //乒乓球不考虑大型多功能活动
            {
                floorHeight = 24;//考虑模数，且净高≥15
            }
            //获取运动场群最小边界
            boundaryMin = new GH_Box(new Box(Plane.WorldXY, new Interval(0, courtTotalWidth), new Interval(0, courtTotalLength), new Interval(0, floorHeight)));
            //获取运动场群最小面积
            groupArea = courtTotalWidth * courtTotalLength;
            #endregion

            #region 绘制每个场地
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < column; j++)
                {
                    List<GH_Point> points = new List<GH_Point>();
                    Point3d downleft;
                    Point3d upright;
                    if (rotation == 0)
                    {
                        downleft = new Point3d(ballCourt.sidelineDistance + (ballCourt.widthPerCourt + ballCourt.sidelineSpacing) * j, ballCourt.terminalDistance + (ballCourt.lengthPerCourt + ballCourt.terminalSpacing) * i, 0);
                        upright = new Point3d(ballCourt.sidelineDistance + ballCourt.widthPerCourt + (ballCourt.widthPerCourt + ballCourt.sidelineSpacing) * j, ballCourt.terminalDistance + ballCourt.lengthPerCourt + (ballCourt.lengthPerCourt + ballCourt.terminalSpacing) * i, 0);
                    }
                    else
                    {
                        downleft = new Point3d(ballCourt.terminalDistance + (ballCourt.lengthPerCourt + ballCourt.terminalSpacing) * j, ballCourt.sidelineDistance + (ballCourt.widthPerCourt + ballCourt.sidelineSpacing) * i, 0);
                        upright = new Point3d(ballCourt.terminalDistance + ballCourt.lengthPerCourt + (ballCourt.lengthPerCourt + ballCourt.terminalSpacing) * j, ballCourt.sidelineDistance + ballCourt.widthPerCourt + (ballCourt.widthPerCourt + ballCourt.sidelineSpacing) * i, 0);
                    }
                    courtOutline.Add(new GH_Rectangle(new Rectangle3d(Plane.WorldXY, downleft, upright)));
                }
            }
            #endregion
        }

        //计算人数
        public override void CalculatePeople(SportsBuilding sportBuilding, BallCourt ballCourt)
        {
            people = (int)(courtNumber * 4 + sportBuilding.spectator * Math.Round(ballCourt.splitRatio));
        }

        //修正球场群组的边界，额外的面积来自于任务书剩余面积的按比例划分，或没有辅助用房，剩余面积均划分给球场
        public void UpdateCourtGroupBoundary(GeneralCourtBuilding generalCourtBuilding)
        {
            if (!generalCourtBuilding.hasAuxiliary)//若并非场馆级，无需单独设置辅助用房群
            {
                //若场地最小值<任务书场馆面积，则求放大系数
                if (groupArea < generalCourtBuilding.area)
                {
                    groupActualArea = generalCourtBuilding.area;
                    ScaleCourt(groupArea, groupActualArea);//扩大场地范围至任务书要求
                }
                else
                { ScaleCourt(groupArea, groupArea); }//仅更新场馆高度
            }
            else//若场馆单独设置辅助用房群
            {
                double detaArea = 0;
                if (courtNumber % 2 == 0)//场地数为偶数，没有多占1块场地
                {
                    detaArea = generalCourtBuilding.reductionRatio * (generalCourtBuilding.generalCourtBuildingGroups[groupNumber].areaRequired - groupArea);
                    groupActualArea = groupArea + detaArea;
                    ScaleCourt(groupArea, groupActualArea);
                }
                else //场地数为奇数，多占1块场地
                {
                    detaArea = generalCourtBuilding.reductionRatio * (generalCourtBuilding.generalCourtBuildingGroups[groupNumber].areaRequired - groupArea + 7.74 * 3.53);
                    groupActualArea = groupArea + detaArea;
                    ScaleCourt(groupArea, groupActualArea);
                }
            }
            //向TableTennisBuildingGroup同步数据
            generalCourtBuilding.generalCourtBuildingGroups[groupNumber].courtAreaRequired = groupArea;
            generalCourtBuilding.generalCourtBuildingGroups[groupNumber].courtAreaActual = groupActualArea;
            //向场馆组汇总boundingBox
            generalCourtBuilding.generalCourtBuildingGroups[groupNumber].groupBoundingBox = boundaryActual.Boundingbox;
            //同步建筑基底面积
            generalCourtBuilding.areaBase.Add(groupActualArea);
        }

        //运动场地根据分配的辅助空间面积缩放
        public void ScaleCourt(double areaBefore, double areaAfter)
        {
            double ratio = 1;
            double detaX = 0;
            double detaY = 0;

            //缩放差值计算
            ratio = Math.Sqrt(areaAfter / areaBefore);
            courtActualWidth = ratio * courtTotalWidth;
            courtActualLength = ratio * courtTotalLength;
            detaX = (courtActualWidth - courtTotalWidth) / 2;
            detaY = (courtActualLength - courtTotalLength) / 2;

            //场馆高度模数化
            GetActualHeight();
            //生成新的场地边界
            boundaryActual = new GH_Box(new Box(Plane.WorldXY, new Interval(-detaX, courtTotalWidth + detaX), new Interval(-detaY, courtTotalLength + detaY), new Interval(0, floorActualHeight)));
        }

        //确定球场排布行列数
        public void GetBallCourtLayout(BallCourt ballCourt)
        {
            switch (ballCourt.courtType)
            {
                #region 篮球训练场
                case CourtType.篮球训练场:
                    multiRowRatio = Tool.GetSplitRatio();
                    if ((courtNumber > 4) && (multiRowRatio > 0.5))
                    {
                        row = 2;
                        column = (int)Math.Ceiling((double)courtNumber / row);
                    }
                    else
                    {
                        row = 1;
                        column = courtNumber;
                    }
                    //确定球场旋转角度
                    if (row == 2)
                    {
                        Tool.IFRotateHalfPie(ref rotation, 0.5);//比double小就不旋转
                    }
                    break;
                #endregion

                #region 羽毛球场
                case CourtType.羽毛球场:
                    multiRowRatio = Tool.GetSplitRatio();
                    if ((courtNumber > 4) && (multiRowRatio > 0.5))
                    {
                        row = 2;
                        column = (int)Math.Ceiling((double)courtNumber / row);
                    }
                    else
                    {
                        row = 1;
                        column = courtNumber;
                    }
                    //确定球场旋转角度
                    if (row == 2)
                    {
                        Tool.IFRotateHalfPie(ref rotation, 1);//比double小就不旋转【！！！暂按不旋转定】
                    }
                    break;
                #endregion

                #region 网球场
                case CourtType.网球场:
                    multiRowRatio = Tool.GetSplitRatio();
                    if ((courtNumber > 2) && (multiRowRatio > 0.5))
                    {
                        row = 2;
                        column = (int)Math.Ceiling((double)courtNumber / row);
                    }
                    else
                    {
                        row = 1;
                        column = courtNumber;
                    }
                    //确定球场旋转角度
                    Tool.IFRotateHalfPie(ref rotation, 0.5);//比double小就不旋转
                    break;
                #endregion

                #region 冰球场
                case CourtType.冰球场:
                    multiRowRatio = 0;
                    row = 1;
                    column = 1;
                    //确定球场旋转角度
                    Tool.IFRotateHalfPie(ref rotation, 0.5);//比double小就不旋转
                    break;
                #endregion

                #region 乒乓球场
                case CourtType.乒乓球场:
                    multiRowRatio = Tool.GetSplitRatio();
                    //拆分为双行的条件
                    if ((courtNumber > 8) && (multiRowRatio > 0.2))
                    {
                        row = 2;
                        column = (int)Math.Ceiling((double)courtNumber / row);
                    }
                    if ((courtNumber <= 8) && (multiRowRatio > 0.5))
                    {
                        row = 2;
                        column = (int)Math.Ceiling((double)courtNumber / row);
                    }
                    else
                    {
                        row = 1;
                        column = courtNumber;
                    }
                    //确定球场旋转角度
                    if (column < 3)
                    {
                        Tool.IFRotateHalfPie(ref rotation, 0.5);//比double小就不旋转
                    }
                    break;
                    #endregion

            }
        }

        //空间位置变动
        public override void Move(Transform transform)
        {
            boundaryActual.Transform(transform);
            for (int i = 0; i < courtOutline.Count; i++)
            {
                courtOutline[i].Transform(transform);
            }
        }
    }
}
