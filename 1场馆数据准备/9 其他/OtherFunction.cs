using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    public class OtherFunction : Building, ITrans
    {
        #region 变量
        //办公平面类型，按走廊位置划分
        public enum FunctionType
        {
            外廊,
            内廊
        }
        //内部变量
        public FunctionType functionType;//走廊类型
        public double columnSpan = 8;//房间进深
        public double unitWidth;//每层作为一个单体，该单体的短边长度
        public double unitMaxArea;//每层最大面积
        public double unitCount;//所需单元数量
        public double heightPerFloor = 6;
        public List<OtherUnits> functionUnits = new List<OtherUnits>();//办公单体组

        public GH_Box groupBoundary;//场馆边界
        public Curve baseBoundary;//基底范围线
        public double baseArea;//基底面积
        public Curve ceilingBoundary;//屋面offset后范围线
        public double ceilingArea;//屋面offset后面积

        //获取朝向
        public Orientation Orientation => orientationBox;
        #endregion

        #region 属性
        //接口的属性
        int ITrans.CurrentLevel
        {
            get { return currentLevel; }
            set { currentLevel = value; }
        }
        int ITrans.ItemIndex
        {
            get { return itemIndex; }
            set { currentLevel = value; }
        }
        #endregion

        //构造函数
        public OtherFunction(double area, Point3d origin)
        {
            StaticObject.otherFunction = this;
            buildingType = BuildingType.其他;
            //外部数据初始化
            this.area = area;
            this.showOrigin = origin;
            //判断办公平面形式
            GetFunctionType();
            //创建办公单元
            CreateOfficeUnit();
        }
        //创建办公单元组
        public void CreateOfficeUnit()
        {
            #region 数据准备
            if (functionType == FunctionType.外廊)
            {
                unitWidth = columnSpan;//单体短边宽度
                unitMaxArea = 500;//根据办公平面类型确定每层办公最大面积

            }
            else//内廊
            {
                unitWidth = columnSpan * 2;//单体短边宽度
                unitMaxArea = 1000;//根据办公平面类型确定每层办公最大面积
            }
            unitCount = Math.Ceiling(area / unitMaxArea);//求所需办公单元数量
            height = heightPerFloor * unitCount;//计算建筑高度
            #endregion

            //创建实体
            for (int i = 0; i < unitCount; i++)
            {
                functionUnits.Add(new OtherUnits(i));
            }
            //各层叠落
            UnitLayout();
            // 获取场馆边界
            GetBoundary();
            //获取随机旋转90度的弧度
            //double rotation = 0;
            //Tool.IFRotateHalfPie(ref rotation, 0.5);
            //Move(Transform.Rotation(rotation, groupBoundary.Value.Center));
            //将场馆单体移至预设点进行展示
            MoveToShowPoint();
        }
        //判断办公平面形式
        public void GetFunctionType()
        {
            if (Tool.GetBool())//外廊
            {
                functionType = FunctionType.外廊;
            }
            else//内廊
            {
                functionType = FunctionType.内廊;
            }
        }
        //排列办公单体
        public void UnitLayout()
        {
            //排列办公单体

            for (int i = 0; i < unitCount; i++)
            {
                functionUnits[i].functionUnit.Transform(Transform.Translation(Vector3d.ZAxis * heightPerFloor * i));
            }
        }
        // 获取场馆边界
        public void GetBoundary()
        {
            //创建场馆单体边界
            #region
            BoundingBox tempBox = functionUnits[0].functionUnit.Boundingbox;
            if (unitCount > 1)
            {
                for (int i = 1; i < unitCount; i++)
                {
                    tempBox = BoundingBox.Union(tempBox, functionUnits[i].functionUnit.Boundingbox);
                }
            }
            groupBoundary = new GH_Box(tempBox);
            #endregion

            //更新相关边界、指标
            GetBaseArea();
        }
        //获取基底面积
        public void GetBaseArea()
        {
            #region 获取底面中心点
            baseCenter = new Point3d(groupBoundary.Value.Center.X, groupBoundary.Value.Center.Y, 0);
            #endregion

            #region 获取基底边界
            //获取运动场地顶面
            List<Brep> breps = new List<Brep>();
            Brep courtBrep = groupBoundary.Value.ToBrep();
            BrepFaceList courtBrepList = courtBrep.Faces;
            Brep courtTop = courtBrepList.ExtractFace(5);
            breps.Add(courtTop);
            //获取顶面的边界曲线
            List<Curve> curveAll = new List<Curve>();//临时存放
            for (int i = 0; i < breps.Count; i++)
            {
                Curve[] tempFrame = breps[i].GetWireframe(0);
                Curve[] tempFrame2 = Curve.JoinCurves(tempFrame);
                tempFrame2[0].MakeClosed(0);
                curveAll.Add(tempFrame2[0]);
            }
            #endregion

            #region 实体搭建
            Curve[] baseBoundaries = Curve.CreateBooleanUnion(curveAll);
            baseBoundaries[0].MakeClosed(0);
            baseBoundary = baseBoundaries[0];
            Curve[] ceiling = baseBoundaries[0].Offset(Plane.WorldXY, StaticObject.offset, 1, CurveOffsetCornerStyle.Sharp);
            ceiling[0].MakeClosed(0);
            ceilingBoundary = ceiling[0];
            baseBoundary.Transform(Transform.Translation(-Vector3d.ZAxis * height));
            #endregion

            #region 数据更新
            baseArea = courtTop.GetArea();
            AreaMassProperties compute = AreaMassProperties.Compute(ceilingBoundary);
            ceilingArea = compute.Area;
            #endregion
        }
        //将场馆单体移至预设点进行展示
        public void MoveToShowPoint()
        {
            Point3d[] points = groupBoundary.Value.GetCorners();
            Transform trans;
            double detaX = groupBoundary.Boundingbox.Max.X - groupBoundary.Boundingbox.Min.X;
            double detaY = groupBoundary.Boundingbox.Max.Y - groupBoundary.Boundingbox.Min.Y;
            if (Math.Abs(detaX) > Math.Abs(detaY))
            {
                trans = Transform.Translation(showOrigin - points[3]);
            }
            else
            {
                trans = Transform.Translation(showOrigin - points[0]);
            }
            Move(trans);
        }
        //移动场馆单体及内部实体
        public void Move(Transform trans)
        {
            groupBoundary.Transform(trans);
            baseBoundary.Transform(trans);
            ceilingBoundary.Transform(trans);
            for (int i = 0; i < unitCount; i++)
            {
                functionUnits[i].functionUnit.Transform(trans);
            }
            baseCenter.Transform(trans);
        }
        //平面布局中的移动
        public void LayoutMove()
        {

        }
        //ITrans接口，实现移动至指定点
        public void SetToPoint(Point3d point3d)
        {
            //构建移动向量
            Point3d[] fromPoint = groupBoundary.Value.GetCorners();
            Transform trans;
            trans = Transform.Translation(point3d - fromPoint[0]);
            //逐一移动实体对象
            Move(trans);
        }
        //将对象拉入边界内
        public int DragIn(GH_Curve curve)
        {
            int failTime = 0;
            int limitation = 5;
            while ((Curve.PlanarCurveCollision(baseBoundary, curve.Value, Plane.WorldXY, 0.1)) && (failTime < limitation))
            {
                Point3d[] vetices = baseBoundary.GetBoundingBox(true).GetCorners();//获取场馆边界点
                List<Vector3d> vectors = new List<Vector3d>();//存储场馆与边线交点
                Vector3d move = Vector3d.Zero;//最终选用的移动变量
                for (int i = 0; i < vetices.Length; i++)
                {
                    if (SiteInfo.siteRetreat.Value.Contains(vetices[i]) == PointContainment.Outside)
                    {
                        double t;
                        SiteInfo.siteRetreat.Value.ClosestPoint(vetices[i], out t);
                        Point3d p = SiteInfo.siteRetreat.Value.PointAt(t);
                        vectors.Add(new Vector3d(p - vetices[i]));
                    }
                }
                foreach (var v in vectors)
                {
                    if (move.Length < v.Length)
                    {
                        move = v;
                    }
                }
                Move(Transform.Translation(move));
                failTime += 1;
            }
            if (failTime > limitation) { return 2; }
            else { return 0; }
        }
        //设置朝向
        public void SetOrientation()
        {
            double xSize = groupBoundary.Boundingbox.Max.X - groupBoundary.Boundingbox.Min.X;
            double ySize = groupBoundary.Boundingbox.Max.Y - groupBoundary.Boundingbox.Min.Y;
            if (xSize < ySize)
            {
                orientationBox = Orientation.垂直;
            }
            else
            {
                orientationBox = Orientation.水平;
            }
        }
        //场馆实体整体移动
        public void Trans(Transform transform)
        {
            Move(transform);
        }
        //旋转
        public void MustRotate(double angle)
        {
            Transform transform = Transform.Rotation(angle, groupBoundary.Value.Center);
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            for (int i = 0; i < unitCount; i++)
            {
                functionUnits[i].functionUnit.Transform(transform);
            }
            baseCenter.Transform(transform);
        }
        //获取底面一半X、Y尺寸,0=X,1=Y
        public double GetHalfBaseSize(int orientation, GH_Box groupB)
        {
            double detaX = groupB.Value.BoundingBox.Diagonal.X / 2;
            double detaY = groupB.Value.BoundingBox.Diagonal.Y / 2;
            if (orientation == 0) { return detaX; }
            else { return detaY; }
        }
        //获取外轮廓box
        public GH_Box GetBoundaryBox()
        {
            return groupBoundary;
        }
        //获取中心点
        public Point3d GetCenterPoint()
        {
            return baseCenter;
        }
        //创建写入JSON用的简化场馆对象
        public SimplifiedBuilding CreateSimplifiedBuilding()
        {
            //获取各层办公用房
            List<GH_Box> gymAuxiliaries = new List<GH_Box>();
            for (int i = 0; i < functionUnits.Count; i++)
            {
                gymAuxiliaries.Add(functionUnits[i].functionUnit);
            }
            //构造新的简化场馆
            return new SimplifiedBuilding(buildingType, groupBoundary.Value.Center, groupBoundary, gymAuxiliaries, new List<GH_Rectangle>());
        }
    }
}
