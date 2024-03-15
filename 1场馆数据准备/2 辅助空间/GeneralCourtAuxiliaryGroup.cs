using Grasshopper.GUI;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //针对除篮球比赛、游泳以外的球类场馆
    public class GeneralCourtAuxiliaryGroup : AuxiliaryGroup
    {
        //构造函数
        public GeneralCourtAuxiliaryGroup(SportsBuildingGroup sportsBuildingGroup, BuildingType buildingType)
        {
            this.sportsBuildingGroup = sportsBuildingGroup;
            this.buildingType = buildingType;
            this.groupNumber = sportsBuildingGroup.groupNumber;
            this.areaRequired = sportsBuildingGroup.areaRequired - sportsBuildingGroup.courtAreaActual;
            sportsBuildingGroup.auxiliaryAreaRequired = areaRequired;
        }
        //生成辅助空间队列
        public void CreateGeneralCourtAuxiliary(GeneralCourtBuildingGroup generalCourtBuildingGroup, double columnDepth)
        {
            //获取场地的8个边界点
            Point3d[] courtCorners = generalCourtBuildingGroup.generalCourtGroup.boundaryActual.Value.GetCorners();
            //获取场地高度
            double heigh = 12;

            //获取辅助空间类型对应的基础数值
            CalculateAuxiliaryAreaStyle(generalCourtBuildingGroup, columnDepth);

            //生成并定位各个辅助空间单元
            if (!generalCourtBuildingGroup.generalCourtBuilding.isMultifuction)//非多功能
            {
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
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));
                        //累加基底面积
                        GetBuilding().areaBase[groupNumber] += columnDepth * length;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长1_2F
                    else if ((areaRequired > areaLong) && (areaRequired <= areaLong * 2))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长1_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        double length = (areaRequired - areaLong) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长2_1F
                    else if ((areaRequired > areaLong * 2) && (areaRequired <= areaLong * 3))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长2_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double length = (areaRequired - areaLong * 2) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长2_2F
                    else if ((areaRequired > areaLong * 3) && (areaRequired <= areaLong * 4))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        double length = (areaRequired - areaLong * 3) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1长2_1F
                    else if ((areaRequired > areaLong * 4) && (areaRequired <= (areaLong * 4 + areaShort + columnDepth * columnDepth * 2)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1长2_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double length = (areaRequired - areaLong * 4) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * 2 + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1长2_2F
                    else if ((areaRequired > (areaLong * 4 + areaShort + columnDepth * columnDepth * 2)) && (areaRequired <= (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 2)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1长2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double lengthAddCorner = boxShort + columnDepth * 2;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        double length = (areaRequired - (areaLong * 4 + areaShort + columnDepth * columnDepth * 2)) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * 2 + lengthAddCorner * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长2_1F
                    else if ((areaRequired > (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 2)) && (areaRequired <= (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 3)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double lengthAddCorner = boxShort + columnDepth * 2;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        double length = (areaRequired - (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 2)) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * 2 + lengthAddCorner * columnDepth + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长2_2F
                    else if ((areaRequired > (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 3)) && (areaRequired <= (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 4)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double lengthAddCorner = boxShort + columnDepth * 2;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        double length = (areaRequired - (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 3)) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * 2 + lengthAddCorner * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长2_2F 超范围
                    else if (areaRequired > (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 4))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double lengthAddCorner = boxShort + columnDepth * 2;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * 2 + lengthAddCorner * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    //同步数据(不超范围)
                    #region
                    if (areaRequired > (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 4))
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 4;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    else
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = areaRequired;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    #endregion
                }
                #region
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
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1_2F
                    else if ((areaRequired > areaShort) && (areaRequired <= areaShort * 2))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        double length = (areaRequired - areaShort) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2_1F
                    else if ((areaRequired > areaShort * 2) && (areaRequired <= areaShort * 3))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double length = (areaRequired - areaShort * 2) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2_2F
                    else if ((areaRequired > areaShort * 3) && (areaRequired <= areaShort * 4))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        double length = (areaRequired - areaShort * 3) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1长1_1F
                    else if ((areaRequired > areaShort * 4) && (areaRequired <= (areaShort + areaShortAndLong)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1长1_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double length = (areaRequired - areaShort * 2) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1长1_2F
                    else if ((areaRequired > (areaShort + areaShortAndLong)) && (areaRequired <= (areaShortAndLong * 2)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1长1_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double lengthAddCorner = boxLong + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        double length = (areaRequired - (areaShort + areaShortAndLong)) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth + lengthAddCorner * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长1_1F
                    else if ((areaRequired > (areaShortAndLong * 2)) && (areaRequired <= (area3Sides + areaShortAndLong)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长1_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double lengthAddCorner = boxLong + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        double length = (areaRequired - (areaShortAndLong * 2)) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth + lengthAddCorner * columnDepth + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长1_2F
                    else if ((areaRequired > (area3Sides + areaShortAndLong)) && (areaRequired <= (area3Sides * 2)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长1_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double lengthAddCorner = boxLong + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        double widthAddCorner = boxShort + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, heigh));//1F
                        double length = (areaRequired - (areaShortAndLong * 2 + areaShort + columnDepth * columnDepth)) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2 + lengthAddCorner * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长2_1F
                    else if ((areaRequired > (area3Sides * 2)) && (areaRequired <= (area3Sides + area4Sides)))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double lengthAddCorner = boxLong + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        double widthAddCorner = boxShort + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, heigh));//2F
                        double length = (areaRequired - area3Sides * 2) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2 + lengthAddCorner * columnDepth + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长2_2F
                    else if (areaRequired > (area3Sides + area4Sides) && (areaRequired <= area4Sides * 2))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double lengthAddCorner = boxLong + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        double widthAddCorner = boxShort + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, heigh));//2F
                        double lengthAddCorners = boxLong + columnDepth * 2;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorners, heigh));//1F
                        double length = (areaRequired - (area3Sides + area4Sides)) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2 + lengthAddCorner * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2长2_2F 超面积
                    else if (areaRequired > area4Sides * 2)
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2长2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double lengthAddCorner = boxLong + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorner, heigh));//2F
                        double widthAddCorner = boxShort + columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, widthAddCorner, heigh));//2F
                        double lengthAddCorners = boxLong + columnDepth * 2;
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorners, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, lengthAddCorners, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2 + lengthAddCorner * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    //同步数据(不超范围)
                    #region
                    if (areaRequired > (areaLong * 4 + (areaShort + columnDepth * columnDepth * 2) * 4))
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = area4Sides * 2;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    else
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = areaRequired;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    #endregion
                }
                #endregion
            }
            else //多功能
            {
                //获取优先布局方位的种子
                double index = Tool.GetRatio();
                //优先在长边布置辅助单元
                if (index > 0.5)
                {
                    #region 长1_1F
                    if ((areaRequired > 0) && (areaRequired <= areaLong))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长1_1F;
                        double length = areaRequired / columnDepth;
                        //获取辅助空间单元
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长1_2F
                    else if ((areaRequired > areaLong) && (areaRequired <= areaLong * 2))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长1_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        double length = (areaRequired - areaLong) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长1_3F
                    else if ((areaRequired > areaLong * 2) && (areaRequired <= areaLong * 3))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长1_3F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double length = (areaRequired - areaLong * 2) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//3F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长1_4F
                    else if ((areaRequired > areaLong * 3) && (areaRequired <= areaLong * 4))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长1_4F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        double length = (areaRequired - areaLong * 3) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//4F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长2_1F
                    else if ((areaRequired > areaLong * 4) && (areaRequired <= areaLong * 5))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长2_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//4F
                        double length = (areaRequired - areaLong * 4) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//1F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长2_2F
                    else if ((areaRequired > areaLong * 5) && (areaRequired <= areaLong * 6))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        double length = (areaRequired - areaLong * 5) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长2_3F
                    else if ((areaRequired > areaLong * 6) && (areaRequired <= areaLong * 7))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长2_3F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        double length = (areaRequired - areaLong * 6) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//3F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长2_4F
                    else if ((areaRequired > areaLong * 7) && (areaRequired <= areaLong * 8))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长2_4F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        double length = (areaRequired - areaLong * 7) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//4F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 长2_4F 超面积
                    else if ((areaRequired > areaLong * 8))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.长2_4F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxLong, heigh));//4F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxLong * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    //同步数据(不超范围)
                    #region
                    if (areaRequired > areaLong * 8)
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = areaLong * 8;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    else
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = areaRequired;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    #endregion
                }

                //优先在短边布置辅助单元
                else
                {
                    #region 短1_1F
                    if ((areaRequired > 0) && (areaRequired <= areaShort))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1_1F;
                        double length = areaRequired / columnDepth;
                        //获取辅助空间单元
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1_2F
                    else if ((areaRequired > areaShort) && (areaRequired <= areaShort * 2))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        double length = (areaRequired - areaShort) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1_3F
                    else if ((areaRequired > areaShort * 2) && (areaRequired <= areaShort * 3))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1_3F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double length = (areaRequired - areaShort * 2) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//3F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短1_4F
                    else if ((areaRequired > areaShort * 3) && (areaRequired <= areaShort * 4))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短1_4F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        double length = (areaRequired - areaShort * 3) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//4F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2_1F
                    else if ((areaRequired > areaShort * 4) && (areaRequired <= areaShort * 5))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2_1F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//4F
                        double length = (areaRequired - areaShort * 4) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//5F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth + length * columnDepth;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2_2F
                    else if ((areaRequired > areaShort * 5) && (areaRequired <= areaShort * 6))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2_2F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        double length = (areaRequired - areaShort * 5) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//2F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2_3F
                    else if ((areaRequired > areaShort * 6) && (areaRequired <= areaShort * 7))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2_3F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        double length = (areaRequired - areaShort * 6) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//3F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2_4F
                    else if ((areaRequired > areaShort * 7) && (areaRequired <= areaShort * 8))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2_4F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        double length = (areaRequired - areaShort * 7) / columnDepth;
                        auxiliary.Add(new Auxiliary(columnDepth, length, heigh));//4F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    #region 短2_4F，超面积
                    else if ((areaRequired > areaShort * 8))
                    {
                        //辅助空间单元布局类型
                        auxiliaryLayoutType = AuxiliaryLayoutType.短2_4F;
                        //创建辅助空间
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//4F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//1F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//2F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//3F
                        auxiliary.Add(new Auxiliary(columnDepth, boxShort, heigh));//4F
                        // 累加基底面积
                        GetBuilding().areaBase[groupNumber] += boxShort * columnDepth * 2;
                        //调整辅助空间位置、朝向
                        AdjustPosition(auxiliary, courtCorners);
                    }
                    #endregion

                    //同步数据(不超范围)
                    #region
                    if (areaRequired > areaShort * 8)
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = areaShort * 8;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    else
                    {
                        generalCourtBuildingGroup.auxiliaryAreaActual = areaRequired;
                        generalCourtBuildingGroup.areaActual = generalCourtBuildingGroup.auxiliaryAreaActual + generalCourtBuildingGroup.courtAreaActual;
                    }
                    #endregion
                }
            }

            //向场馆组汇总boundingBox
            for (int i = 0; i < auxiliary.Count; i++)
            {
                this.sportsBuildingGroup.groupBoundingBox.Union(auxiliary[i].auxiliaryUnit.Boundingbox);
            }
        }
        //获取辅助空间类型对应的基础数值
        public void CalculateAuxiliaryAreaStyle(GeneralCourtBuildingGroup generalCourtBuildingGroup, double columnDepth)
        {
            //获取辅助用房理论值范围，初始化辅助空间形态
            boxWidth = generalCourtBuildingGroup.generalCourtGroup.boundaryActual.Value.X.Length;
            boxLength = generalCourtBuildingGroup.generalCourtGroup.boundaryActual.Value.Y.Length;
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
            //计算球场周边面积类型对应边界值
            areaShort = boxShort * columnDepth;
            areaLong = boxLong * columnDepth;
            areaShortAndLong = areaShort + areaLong + columnDepth * columnDepth;
            area3Sides = areaShort * 2 + areaLong + columnDepth * columnDepth * 2;
            area4Sides = (areaShort + areaLong + columnDepth * columnDepth * 2) * 2;
        }
        //获取此类场馆对应的管理类
        public GeneralCourtBuilding GetBuilding()
        {
            GeneralCourtBuilding temp = null;
            switch (buildingType)
            {
                case BuildingType.篮球训练馆:
                    temp = StaticObject.basketballTrainingBuilding;
                    break;
                case BuildingType.网球馆:
                    temp = StaticObject.tennisBuilding;
                    break;
                case BuildingType.羽毛球馆:
                    temp = StaticObject.badmintonBuilding;
                    break;
                case BuildingType.冰球馆:
                    temp = StaticObject.iceHockeyBuilding;
                    break;
                case BuildingType.乒乓球馆:
                    temp = StaticObject.tableTennisBuilding;
                    break;
            }
            return temp;
        }
        //初始化辅助用房位置
        public void AdjustPosition(List<Auxiliary> auxiliary, Point3d[] courtCorners)
        {

            if (!GetBuilding().isMultifuction)//非多功能
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
                else if ((widthDivideLength >= 1) && (widthDivideLength < 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_2F)))
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
                else if ((widthDivideLength >= 1) && (widthDivideLength < 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_2F)))
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

                #region 短1长1_1F、短1长1_2F
                //场地boundaryX轴<Y轴+短1长1_1F、短1长1_2F
                else if ((widthDivideLength > 0.5) && (widthDivideLength < 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_2F)))
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
                //场地boundaryX轴>=Y轴+短1长1_1F、短1长1_2F
                else if ((widthDivideLength >= 1) && (widthDivideLength < 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1长1_2F)))
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
                else if ((widthDivideLength >= 1) && (widthDivideLength < 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长1_2F)))
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
                else if ((widthDivideLength >= 1) && (widthDivideLength < 2) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2长2_2F)))
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
            else // 多功能使用
            {
                #region 长1_1F至长1_4F
                //场地XY比＜=1,长1_1F至长1_4F
                if ((widthDivideLength <= 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_4F)))
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
                //场地XY比 >1，长1_1F至长1_4F
                else if ((widthDivideLength > 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长1_4F)))
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
                #endregion

                #region 长2_1F至长1_4F
                //场地XY比＜=1,长2_1F至长2_4F
                else if ((widthDivideLength <= 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_4F)))
                {
                    for (int i = 0; i < auxiliary.Count; i++)
                    {
                        //获取辅助空间单元角点
                        Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                        //生成移动向量
                        Vector3d moveVector = new Vector3d();
                        if (i % 2 == 0)//偶数
                        {
                            moveVector = courtCorners[3] - auxiliaryCorners[2] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        else//奇数
                        {
                            moveVector = courtCorners[2] - auxiliaryCorners[3] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        Transform move = Transform.Translation(moveVector);
                        auxiliary[i].auxiliaryUnit.Transform(move);
                    }
                }
                //场地XY比 >1，长2_1F至长2_4F
                else if ((widthDivideLength > 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.长2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.长2_4F)))
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
                        if (i % 2 == 0)
                        {
                            moveVector = courtCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        else
                        {
                            moveVector = courtCorners[3] - auxiliaryCorners[3] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        Transform move = Transform.Translation(moveVector);
                        auxiliary[i].auxiliaryUnit.Transform(move);
                        auxiliary[i].rotation = Tool.AngleToRadians(90);
                    }
                }
                #endregion

                #region 短1_1F至短1_4F
                //场地boundaryX轴<Y轴+短1_1F至短1_4F
                else if ((widthDivideLength <= 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_4F)))
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
                //场地boundaryX轴>Y轴+短1_1F至短1_4F
                else if ((widthDivideLength > 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短1_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短1_4F)))
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

                #region 短2_1F至短2_4F
                //场地boundaryX轴<Y轴+短2_1F至短2_4F
                else if ((widthDivideLength <= 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_4F)))
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
                        if (i % 2 == 0)
                        {
                            moveVector = courtCorners[0] - auxiliaryCorners[2] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        else
                        {
                            moveVector = courtCorners[3] - auxiliaryCorners[3] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        Transform move = Transform.Translation(moveVector);
                        auxiliary[i].auxiliaryUnit.Transform(move);
                    }
                }
                //场地boundaryX轴>=Y轴+短2_1F至短2_4F
                else if ((widthDivideLength > 1) && ((auxiliaryLayoutType == AuxiliaryLayoutType.短2_1F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_2F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_3F) || (auxiliaryLayoutType == AuxiliaryLayoutType.短2_4F)))
                {
                    for (int i = 0; i < auxiliary.Count; i++)
                    {
                        //获取辅助空间单元角点
                        Point3d[] auxiliaryCorners = auxiliary[i].auxiliaryUnit.Value.GetCorners();
                        //生成移动向量
                        Vector3d moveVector = new Vector3d();
                        if (i % 2 == 0)
                        {
                            moveVector = courtCorners[3] - auxiliaryCorners[2] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        else
                        {
                            moveVector = courtCorners[2] - auxiliaryCorners[3] + new Vector3d(0, 0, (i / 2) * auxiliary[i].height);
                        }
                        Transform move = Transform.Translation(moveVector);
                        auxiliary[i].auxiliaryUnit.Transform(move);
                    }
                }
                #endregion
            }
        }
    }
}
