using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //健身馆某个场馆
    public class GymnasiumGroup : SportsBuildingGroup, ITrans
    {
        #region 变量
        public GymCourt gymCourt;
        public GymAuxiliaryGroup gymAuxiliaryGroup;//辅助功能组


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
            set { itemIndex = value; }
        }
        //获取朝向
        public Orientation Orientation => orientationBox;
        #endregion
        #endregion

        //构造函数
        public GymnasiumGroup(int groupNumber)
        {
            this.groupNumber = groupNumber;
            buildingType = BuildingType.健身馆;
            //创建运动场地、辅助空间组
            areaRequired = StaticObject.gymnasium.areaTotalGroupActual[groupNumber];
            courtAreaRequired = StaticObject.gymnasium.areaCourtGroupActual[groupNumber];
            gymCourt = new GymCourt(groupNumber);
        }

        //创建辅助用房，因为不能在构造完成之前调用正在创建对象，即无法调用正在创建的场地对象
        public void CreateGymAuxiliaryGroup()
        {
            if (StaticObject.gymnasium.hasAuxiliary)
            {
                gymAuxiliaryGroup = new GymAuxiliaryGroup(groupNumber);
                CreateGroupBoundary();
            }
            else
            {
                groupBoundary = gymCourt.gymCourt;
            }
        }

        //创建场馆单体边界
        public void CreateGroupBoundary()
        {
            #region 创建场馆单体边界
            //获取球场的boundingbox
            groupBoundary = new GH_Box(gymCourt.gymCourt);

            //如果有辅助用房，将范围扩展至辅助用房
            BoundingBox tempBox = new BoundingBox();
            if (StaticObject.gymnasium.hasAuxiliary)
            {
                for (int i = 0; i < gymAuxiliaryGroup.auxiliary.Count; i++)
                {
                    //若该辅助用房位于0标高，则计入baseAreaIdeal面积
                    if (gymAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Boundingbox.Min.Z == 0)
                    {
                        baseAreaIdeal += gymAuxiliaryGroup.auxiliary[i].area;
                    }
                    //将辅助空间添加至整体BoundingBox中
                    tempBox = BoundingBox.Union(groupBoundary.Boundingbox, gymAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Boundingbox);
                }
                Interval x = new Interval(tempBox.Min.X, tempBox.Max.X);
                Interval y = new Interval(tempBox.Min.Y, tempBox.Max.Y);
                groupBoundary = new GH_Box(new Box(Plane.WorldXY, x, y, new Interval(0, StaticObject.gymnasium.height)));
            }
            #endregion

            #region 获取底面中心点
            baseCenter = new Point3d(groupBoundary.Value.Center.X, groupBoundary.Value.Center.Y, 0);
            #endregion

            //更新相关边界、指标
            #region 构建底面、顶面边界
            //以运动场馆groupBoundary的底面为底边线
            Brep baseBrep = groupBoundary.Value.ToBrep();
            BrepFaceList baseBrepList = baseBrep.Faces;
            Brep baseBrep2 = baseBrepList.ExtractFace(5);
            Curve[] baseFrame = baseBrep2.GetWireframe(0);
            Curve[] baseFrame2 = Curve.JoinCurves(baseFrame);
            baseFrame2[0].MakeClosed(0);
            baseFrame2[0].Transform(Transform.Translation(-Vector3d.ZAxis * baseFrame2[0].GetBoundingBox(true).Max.Z));
            baseBoundary = baseFrame2[0];//指定底面边界
            Curve[] ceilingOffset = baseBoundary.Offset(Plane.WorldXY, StaticObject.offset, 1, CurveOffsetCornerStyle.Sharp);
            ceilingOffset[0].MakeClosed(0);//获取顶面轮廓线
            ceilingBoundary = ceilingOffset[0];
            ceilingBoundary.Transform(Transform.Translation(Vector3d.ZAxis * groupBoundary.Boundingbox.Max.Z));//移动到顶面标高
            #endregion

            #region 数据更新
            //更新顶面积、底面积
            baseArea = baseBrep2.GetArea();
            AreaMassProperties compute = AreaMassProperties.Compute(ceilingBoundary);
            ceilingArea = compute.Area;
            //添加由于辅助空间不满而确实的首层面积，获得场馆布局之前真实场馆面积
            double areaDifferent = baseArea - baseAreaIdeal;
            areaActual += areaDifferent;
            #endregion
        }
        //场馆整体移动
        public void Move(Transform transform)
        {
            #region 场馆
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            baseCenter.Transform(transform);
            #endregion

            #region 场地
            gymCourt.gymCourt.Transform(transform);
            #endregion

            #region 辅助
            if (gymAuxiliaryGroup != null)
            {
                foreach (var item in gymAuxiliaryGroup.auxiliary)
                {
                    item.auxiliaryUnit.Transform(transform);
                }
            }
            #endregion
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
            moveDelegate(trans);
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
                moveDelegate(Transform.Translation(move));
                failTime += 1;
            }
            if (failTime > limitation) { return 2; }
            else { return 0; }
        }
        //场馆实体整体移动
        public void Trans(Transform transform)
        {
            moveDelegate(transform);
        }
        //旋转
        public void MustRotate(double angle)
        {
            Transform transform = Transform.Rotation(angle, groupBoundary.Value.Center);

            #region 场馆
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            baseCenter.Transform(transform);
            #endregion

            #region 场地
            gymCourt.gymCourt.Transform(transform);
            #endregion

            #region 辅助
            if (gymAuxiliaryGroup != null)
            {
                foreach (var item in gymAuxiliaryGroup.auxiliary)
                {
                    item.auxiliaryUnit.Transform(transform);
                }
            }
            #endregion
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
            //获取辅助用房
            List<GH_Box> gymAuxiliaries = new List<GH_Box>();
            for (int i = 0; i < gymAuxiliaryGroup.auxiliary.Count; i++)
            {
                gymAuxiliaries.Add(gymAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
            }
            //构造新的简化场馆
            return new SimplifiedBuilding(buildingType, groupBoundary.Value.Center, groupBoundary, gymAuxiliaries, new List<GH_Rectangle>());
        }
    }
}
