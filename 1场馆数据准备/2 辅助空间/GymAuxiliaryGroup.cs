using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    public class GymAuxiliaryGroup : AuxiliaryGroup
    {
        //构造函数
        public GymAuxiliaryGroup(int groupNumber)
        {
            this.groupNumber = groupNumber;
            areaRequired = StaticObject.gymnasium.areaAuxiliaryGroupActual[groupNumber];
            CreateGymAuxiliary();
        }
        //生成辅助空间队列
        public void CreateGymAuxiliary()
        {
            //获取场地的8个边界点
            Point3d[] courtCorners = StaticObject.gymnasium.gymnasiumGroup[groupNumber].gymCourt.gymCourt.Value.GetCorners();
            //获取场地高度
            double height = StaticObject.gymnasium.gymnasiumGroup[groupNumber].gymCourt.height;
            double columnDepth = StaticObject.gymnasium.auxiliaryColumnSpan;

            //获取辅助空间类型对应的基础数值
            CalculateAuxiliaryAreaStyle();

            //生成并定位各个辅助空间单元
            //长宽>2或＜0.5，优先在长边布置辅助单元
            if ((widthDivideLength > 2 || widthDivideLength < 0.5))
            {

                #region 长1_1F
                if ((areaRequired > 0) && (areaRequired <= areaLong))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.长1_1F;
                    double length = areaRequired / columnDepth;
                    //获取辅助空间单元
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

                #region 长2_1F
                else if ((areaRequired > areaLong) && (areaRequired <= areaLong * 2))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.长2_1F;
                    //创建辅助空间
                    auxiliary.Add(new Auxiliary(columnDepth, boxLong, height, true));//1F
                    double length = (areaRequired - areaLong) / columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));//1F
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += areaLong + columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

                #region 短1长2_1F
                else if ((areaRequired > areaLong * 2) && (areaRequired <= (areaLong * 2 + areaShort + columnDepth * columnDepth * 2)))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.短1长2_1F;
                    //创建辅助空间
                    auxiliary.Add(new Auxiliary(columnDepth, boxLong, height, true));//1F
                    auxiliary.Add(new Auxiliary(columnDepth, boxLong, height, true));//1F
                    double length = (areaRequired - areaLong * 2) / columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));//1F
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += areaLong * 2 + columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

                #region 短2长2_1F
                else if ((areaRequired > (areaLong * 2 + areaShort + columnDepth * columnDepth * 2)) && (areaRequired <= area4Sides))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_1F;
                    //创建辅助空间
                    auxiliary.Add(new Auxiliary(columnDepth, boxLong, height, true));//1F
                    auxiliary.Add(new Auxiliary(columnDepth, boxLong, height, true));//1F
                    double lengthAddCorner = boxShort + columnDepth * 2;
                    auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, height, true));//1F
                    double length = (areaRequired - (areaLong * 2 + areaShort + columnDepth * columnDepth * 2)) / columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));//1F
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += areaLong * 2 + areaShort + columnDepth * columnDepth * 2 + columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

            }
            //长宽比＜=2且＞=0.5，优先在短边布置辅助单元
            else
            {
                #region 短1_1F
                if ((areaRequired > 0) && (areaRequired <= areaShort))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.短1_1F;
                    double length = areaRequired / columnDepth;
                    //获取辅助空间单元
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

                #region 短2_1F
                else if ((areaRequired > areaShort) && (areaRequired <= areaShort * 2))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.短2_1F;
                    //创建辅助空间
                    auxiliary.Add(new Auxiliary(columnDepth, boxShort, height, true));//1F
                    double length = (areaRequired - areaShort) / columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));//2F
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += areaShort + columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

                #region 短1长1_1F
                else if ((areaRequired > areaShort * 2) && (areaRequired <= (areaShortAndLong)))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.短1长1_1F;
                    //创建辅助空间
                    auxiliary.Add(new Auxiliary(columnDepth, boxShort, height, true));//1F
                    auxiliary.Add(new Auxiliary(columnDepth, boxShort, height, true));//2F
                    double length = (areaRequired - areaShort * 2) / columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));//1F
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += areaShort * 2 + columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

                #region 短2长1_1F
                else if ((areaRequired > areaShortAndLong) && (areaRequired <= area3Sides))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.短2长1_2F;
                    //创建辅助空间
                    auxiliary.Add(new Auxiliary(columnDepth, boxShort, height, true));//1F
                    double lengthAddCorner = boxLong + columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, height, true));//1F
                    double length = (areaRequired - areaShortAndLong) / columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));//1F
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += areaShortAndLong + columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion

                #region 短2长2_1F
                else if ((areaRequired > area3Sides) && (areaRequired <= area4Sides))
                {
                    //辅助空间单元布局类型
                    auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_1F;
                    //创建辅助空间
                    auxiliary.Add(new Auxiliary(columnDepth, boxShort, height, true));//1F
                    double lengthAddCorner = boxLong + columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, height, true));//1F
                    double widthAddCorner = boxShort + columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, height, true));//1F
                    double length = (areaRequired - area3Sides) / columnDepth;
                    auxiliary.Add(new Auxiliary(columnDepth, length, height, true));//1F
                    //更新基底面积
                    StaticObject.gymnasium.areaBase[groupNumber] += area3Sides + columnDepth * length;
                    //调整辅助空间位置、朝向
                    AdjustPosition(auxiliary, courtCorners);
                }
                #endregion
            }

        }
        //获取辅助空间类型对应的基础数值
        public void CalculateAuxiliaryAreaStyle()
        {
            //获取辅助用房理论值范围，初始化辅助空间形态
            boxWidth = StaticObject.gymnasium.gymnasiumGroup[groupNumber].gymCourt.widthPerCourt;
            boxLength = StaticObject.gymnasium.gymnasiumGroup[groupNumber].gymCourt.lengthPerCourt;
            widthDivideLength = boxWidth / boxLength;
            if (widthDivideLength < 1)
            {
                boxShort = boxWidth;
                boxLong = boxLength;
            }
            else
            {
                boxShort = boxLength;
                boxLong = boxWidth;
            }
            double columnDepth = StaticObject.gymnasium.auxiliaryColumnSpan;

            //计算球场周边面积类型对应边界值
            areaShort = boxShort * columnDepth;
            areaLong = boxLong * columnDepth;
            areaShortAndLong = areaShort + areaLong + columnDepth * columnDepth;
            area3Sides = areaShort * 2 + areaLong + columnDepth * columnDepth * 2;
            area4Sides = (areaShort + areaLong + columnDepth * columnDepth * 2) * 2;
        }
        //初始化辅助用房位置
        public void AdjustPosition(List<Auxiliary> auxiliary, Point3d[] courtCorners)
        {
            #region 长1_1F、长1_2F
            //场地XY比＜0.5,长1_1F、长1_2F
            if ((widthDivideLength < 0.5) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = courtCorners[3] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地XY比>2，长1_1F、长1_2F
            else if ((widthDivideLength > 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //旋转辅助空间
                    Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                    auxiliary[i].auxiliaryUnit.Transform(rotate);
                    auxiliary[i].rotation = Tool.AngleToRadians(90);
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = courtCorners[0] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion

            #region 长2_1F，长2_2F
            //场地XY比＜0.5,长2_1F，长2_2F
            else if ((widthDivideLength < 0.5) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else
                    {
                        moveVector = courtCorners[2] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地XY比>2，长2_1F，长2_2F
            else if ((widthDivideLength > 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //旋转辅助空间
                    Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                    auxiliary[i].auxiliaryUnit.Transform(rotate);
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[0] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                    auxiliary[i].rotation = Tool.AngleToRadians(90);
                }
            }
            #endregion

            #region 短1长2_1F、短1长2_2F
            //场地XY比＜0.5,短1长2_1F、短1长2_2F
            else if ((widthDivideLength < 0.5) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else if (i < 4)
                    {
                        moveVector = courtCorners[2] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    else
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();//更新旋转后角点坐标
                        //获取auxiliary[0]的0号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地XY比>2，短1长2_1F、短1长2_2F
            else if ((widthDivideLength > 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    Vector3d moveVector = new Vector3d();
                    if (i < 4)
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        //获取辅助空间单元角点
                        Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                        //生成移动向量
                        if (i < 2)
                        {
                            moveVector = courtCorners[0] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                        }
                        else
                        {
                            moveVector = courtCorners[3] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                        }
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    else
                    {
                        //获取辅助空间单元角点
                        Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                        //获取auxiliary[0]的3号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion

            #region 短2长2_1F、短2长2_2F
            //场地XY比＜0.5,短2长2_1F、短2长2_2F
            else if ((widthDivideLength < 0.5) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else if (i < 4)
                    {
                        moveVector = courtCorners[2] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    else
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();//更新旋转后角点坐标
                        //获取auxiliary[0]的0号/3号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        if (i < 6)
                        {
                            moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                        }
                        else
                        {
                            moveVector = FirstAuxiliaryUnitCorners[3] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 6) * auxiliary[i].height);
                        }
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地XY比>2，短2长2_1F、短2长2_2F
            else if ((widthDivideLength > 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    Vector3d moveVector = new Vector3d();
                    if (i < 4)
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    if (i < 2)
                    {
                        moveVector = courtCorners[0] - auxiliaryCorners[2] + i * new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else if (i < 4)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    else if (i < 6)
                    {
                        //获取auxiliary[0]的3号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[3] - auxiliaryCorners[1] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                    }
                    else
                    {
                        //获取auxiliary[0]的1号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 6) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion

            #region 短1_1F、短1_2F
            //场地boundaryX轴<Y轴+短1_1F、短1_2F
            else if ((widthDivideLength >= 0.5) && (widthDivideLength < 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //旋转辅助空间
                    Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                    auxiliary[i].auxiliaryUnit.Transform(rotate);
                    auxiliary[i].rotation = Tool.AngleToRadians(90);
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = courtCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地boundaryX轴>=Y轴+短1_1F、短1_2F
            else if ((widthDivideLength >= 1) && (widthDivideLength < 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = courtCorners[3] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion

            #region 短2_1F、短2_2F
            //场地boundaryX轴<Y轴+短2_1F、短2_2F
            else if ((widthDivideLength >= 0.5) && (widthDivideLength < 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //旋转辅助空间
                    Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                    auxiliary[i].auxiliaryUnit.Transform(rotate);
                    auxiliary[i].rotation = Tool.AngleToRadians(90);
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地boundaryX轴>=Y轴+短2_1F、短2_2F
            else if ((widthDivideLength >= 1) && (widthDivideLength <= 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else
                    {
                        moveVector = courtCorners[2] - auxiliaryCorners[3] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion

            #region 短1长1_1F、短1长1_1F
            //场地boundaryX轴<Y轴+短1长1_1F、短1长1_1F
            else if ((widthDivideLength >= 0.5) && (widthDivideLength < 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    if (i < 2)
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else
                    {
                        //获取auxiliary[0]的3号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地boundaryX轴>=Y轴+短1长1_1F、短1长1_1F
            else if ((widthDivideLength >= 1) && (widthDivideLength <= 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    if (i > 1)
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else
                    {
                        //获取auxiliary[0]的3号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion

            #region 短2长1_1F、短2长1_1F
            //场地boundaryX轴<Y轴+短2长1_1F、短2长1_1F
            else if ((widthDivideLength >= 0.5) && (widthDivideLength < 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    if ((i < 2) || (i > 3))
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else if (i < 4)
                    {
                        //获取auxiliary[0]的0号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    else
                    {
                        //获取auxiliary[2]的2号角点
                        Point3d[] ThirdAuxiliaryUnitCorners = auxiliary[2].auxiliaryUnit.Value.GetCorners();
                        moveVector = ThirdAuxiliaryUnitCorners[2] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地boundaryX轴>=Y轴+短1长1_1F、短1长1_1F
            else if ((widthDivideLength >= 1) && (widthDivideLength <= 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长1_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    if ((i > 1) && (i < 4))
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else if (i < 4)
                    {
                        //获取auxiliary[0]的3号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    else
                    {
                        //获取auxiliary[2]的0号角点
                        Point3d[] ThirdAuxiliaryUnitCorners = auxiliary[2].auxiliaryUnit.Value.GetCorners();
                        moveVector = ThirdAuxiliaryUnitCorners[0] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion

            #region 短2长2_1F、短2长2_2F
            //场地boundaryX轴<Y轴+短2长2_1F、短2长2_2F
            else if ((widthDivideLength >= 0.5) && (widthDivideLength < 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    if ((i < 2) || ((i > 3) && (i < 6)))
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else if (i < 4)
                    {
                        //获取auxiliary[0]的0号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    else if (i < 6)
                    {
                        //获取auxiliary[2]的2号角点
                        Point3d[] ThirdAuxiliaryUnitCorners = auxiliary[2].auxiliaryUnit.Value.GetCorners();
                        moveVector = ThirdAuxiliaryUnitCorners[2] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                    }
                    else
                    {
                        //获取auxiliary[4]的3号角点
                        Point3d[] ThirdAuxiliaryUnitCorners = auxiliary[4].auxiliaryUnit.Value.GetCorners();
                        moveVector = ThirdAuxiliaryUnitCorners[2] - auxiliaryCorners[2] + new Vector3d(0, 0, (i - 6) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            //场地boundaryX轴>=Y轴+短2长2_1F、短2长2_2F
            else if ((widthDivideLength >= 1) && (widthDivideLength <= 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_2F)))
            {
                for (int i = 0; i < auxiliary.Count; i++)
                {
                    if (((i > 1) && (i < 4)) || (i > 5) && (i < 8))
                    {
                        //旋转辅助空间
                        Transform rotate = Transform.Rotation(Tool.AngleToRadians(90), Vector3d.ZAxis, auxiliary[i].auxiliaryUnit.Value.BoundingBox.Center);
                        auxiliary[i].auxiliaryUnit.Transform(rotate);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                    //获取辅助空间单元角点
                    Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                    //生成移动向量
                    Vector3d moveVector = new Vector3d();
                    if (i < 2)
                    {
                        moveVector = courtCorners[3] - auxiliaryCorners[2] + new Vector3d(0, 0, i * auxiliary[i].height);
                    }
                    else if (i < 4)
                    {
                        //获取auxiliary[0]的3号角点
                        Point3d[] FirstAuxiliaryUnitCorners = auxiliary[0].auxiliaryUnit.Value.GetCorners();
                        moveVector = FirstAuxiliaryUnitCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, (i - 2) * auxiliary[i].height);
                    }
                    else if (i < 6)
                    {
                        //获取auxiliary[2]的0号角点
                        Point3d[] ThirdAuxiliaryUnitCorners = auxiliary[2].auxiliaryUnit.Value.GetCorners();
                        moveVector = ThirdAuxiliaryUnitCorners[0] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 4) * auxiliary[i].height);
                    }
                    else
                    {
                        //获取auxiliary[4]的0号角点
                        Point3d[] ThirdAuxiliaryUnitCorners = auxiliary[4].auxiliaryUnit.Value.GetCorners();
                        moveVector = ThirdAuxiliaryUnitCorners[2] - auxiliaryCorners[0] + new Vector3d(0, 0, (i - 6) * auxiliary[i].height);
                    }
                    Transform move = Transform.Translation(moveVector);
                    auxiliary[i].auxiliaryUnit.Transform(move);
                }
            }
            #endregion
        }
    }
}
