using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //健身馆数据管理类
    public class Gymnasium : SportsBuilding
    {
        //内部变量
        public double columnSpan = 8;//运动场柱距模数
        public double courtWidth;//运动场短边长度
        public double auxiliaryColumnSpan = 8;//辅助空间柱距
        public double splitRatio = 1;

        public List<GymnasiumGroup> gymnasiumGroup = new List<GymnasiumGroup>();

        //构造函数
        public Gymnasium(double area, bool hasAuxiliary, double courtRatio, Point3d origin)
        {
            StaticObject.gymnasium = this;
            buildingType = BuildingType.健身馆;
            //外部数据初始化
            this.area = area;
            this.hasAuxiliary = hasAuxiliary;
            this.reductionRatio = courtRatio;//获取运动场地与场馆建筑面积的比
            this.showOrigin = origin;
            this.height = 6;

            //生成运动场地信息+场馆对象
            CreateGymCourt();
            //创建辅助用房
            for (int i = 0; i < gymnasiumGroup.Count; i++)
            {
                gymnasiumGroup[i].CreateGymAuxiliaryGroup();
            }
            //随机旋转场馆90度
            for (int i = 0; i < gymnasiumGroup.Count; i++)
            {
                Move(GetRotateTransform(gymnasiumGroup[i].groupBoundary.Value.Center), i);
            }

            //将场馆单体移动至展示位置
            MoveToShowPoint();

            //注册到移动代理
            for (int i = 0; i < gymnasiumGroup.Count; i++)
            {
                gymnasiumGroup[i].moveDelegate += gymnasiumGroup[i].Move;
            }
        }
        //生成运动场地信息
        public void CreateGymCourt()
        {

            #region 获取运动场短边长度
            double index = Tool.GetRatio();
            if (index > 0.5)
            { courtWidth = 16; }
            else
            { courtWidth = 24; }
            #endregion

            #region 判断是否对场馆进行拆分，若拆分，则拆成2个
            double ifSplit = Tool.GetRatio();
            splitRatio = Tool.GetSpecificDouble(4, 7);//获取拆分场馆的面积比
            if ((ifSplit > 0.5) && (area < 2000))//不拆分
            {
                areaTotalGroupRequired.Add(area);
                areaTotalGroupActual.Add(area);//计算总面积
                if (hasAuxiliary)
                {
                    areaCourtGroupRequired.Add(area * reductionRatio);
                    areaCourtGroupActual.Add(area * reductionRatio);//计算运动场地面积
                    areaAuxiliaryGroupRequired.Add(area * (1 - reductionRatio));
                    areaAuxiliaryGroupActual.Add(area * (1 - reductionRatio));//计算辅助用房面积
                    areaBase.Add(area * reductionRatio);//获取基底面积
                }
                else
                {
                    areaCourtGroupRequired.Add(area);
                    areaCourtGroupActual.Add(area);//计算运动场地面积
                    areaBase.Add(area);//获取基底面积
                }
                gymnasiumGroup.Add(new GymnasiumGroup(0));
                gymnasiumGroup[0].baseAreaIdeal += areaBase[0];
            }
            else//拆分为2个
            {
                //计算总面积
                areaTotalGroupRequired.Add(area * splitRatio);
                areaTotalGroupActual.Add(area * splitRatio);
                areaTotalGroupRequired.Add(area * (1 - splitRatio));
                areaTotalGroupActual.Add(area * (1 - splitRatio));
                //计算运动场地面积
                areaCourtGroupRequired.Add(areaTotalGroupActual[0] * reductionRatio);
                areaCourtGroupActual.Add(areaTotalGroupActual[0] * reductionRatio);
                areaBase.Add(areaTotalGroupActual[0] * reductionRatio);
                areaCourtGroupRequired.Add(areaTotalGroupActual[1] * reductionRatio);
                areaCourtGroupActual.Add(areaTotalGroupActual[1] * reductionRatio);
                areaBase.Add(areaTotalGroupActual[1] * reductionRatio);
                //计算辅助用房面积
                areaAuxiliaryGroupRequired.Add(areaTotalGroupActual[0] * (1 - reductionRatio));
                areaAuxiliaryGroupActual.Add(areaTotalGroupActual[0] * (1 - reductionRatio));
                areaAuxiliaryGroupRequired.Add(areaTotalGroupActual[1] * (1 - reductionRatio));
                areaAuxiliaryGroupActual.Add(areaTotalGroupActual[1] * (1 - reductionRatio));
                gymnasiumGroup.Add(new GymnasiumGroup(0));
                gymnasiumGroup.Add(new GymnasiumGroup(1));
                //获取球场的底面面积
                gymnasiumGroup[0].baseAreaIdeal += areaBase[0];
                gymnasiumGroup[1].baseAreaIdeal += areaBase[1];
                gymnasiumGroup[0].areaActual += areaTotalGroupActual[0];
                gymnasiumGroup[1].areaActual += areaTotalGroupActual[1];
            }
            #endregion

        }
        //获得旋转Trans
        public Transform GetRotateTransform(Point3d rotationCenter)
        {
            double rotation = 0;
            Tool.IFRotateHalfPie(ref rotation, 0.5);
            return Transform.Rotation(rotation, rotationCenter);
        }

        //将场馆单体移至预设点进行展示
        public void MoveToShowPoint()
        {
            Point3d fromPointGroup1 = gymnasiumGroup[0].groupBoundary.Boundingbox.Min;//获取场馆边界左下点
            Transform transToShowPoint = Transform.Translation(showOrigin - fromPointGroup1);//移动至指定点的Trans
            Move(transToShowPoint, 0);
            if (gymnasiumGroup.Count > 1)
            {
                Point3d fromPointGroup2 = gymnasiumGroup[1].groupBoundary.Boundingbox.Min;//获取场馆边界左下点
                double deta = gymnasiumGroup[0].groupBoundary.Boundingbox.Max.X - gymnasiumGroup[0].groupBoundary.Boundingbox.Min.X;
                Vector3d v = showOrigin - fromPointGroup2 + (new Vector3d(deta + 50, 0, 0));
                transToShowPoint = Transform.Translation(v);//移动至指定点的Trans
                Move(transToShowPoint, 1);
            }

        }
        //移动场馆单体及内部实体
        public void Move(Transform trans, int groupNumber)
        {
            gymnasiumGroup[groupNumber].groupBoundary.Transform(trans);//场馆外轮廓
            gymnasiumGroup[groupNumber].gymCourt.gymCourt.Transform(trans);//泳池外轮廓
            if (hasAuxiliary)
            {
                foreach (var item in gymnasiumGroup[groupNumber].gymAuxiliaryGroup.auxiliary)//辅助用房
                {
                    item.auxiliaryUnit.Transform(trans);
                }

            }
            gymnasiumGroup[groupNumber].baseBoundary.Transform(trans);
            gymnasiumGroup[groupNumber].ceilingBoundary.Transform(trans);
            gymnasiumGroup[groupNumber].baseCenter.Transform(trans);
        }
    }
}
