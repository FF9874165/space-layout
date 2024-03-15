using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    //建筑单体类，充当父类
    public class BuildingGroup
    {

        public Move moveDelegate;
        public Transform trans;
        //实体变量
        public BoundingBox groupBoundingBox;
        public Orientation orientationBox;//朝向
        public GH_Box groupBoundary;
        public Curve baseBoundary;//基底范围线
        public double baseArea;//基底面积
        public double baseAreaIdeal = 0;//基底面积理想面积，及不考虑辅助空间未占满的区域
        public Curve ceilingBoundary;//屋面offset后范围线
        public double ceilingArea;//屋面offset后面积
        public Point3d baseCenter;//底面中心店，用于计算场馆间位置关系

        //多播控制运动场地、辅助用房移动
        public delegate void Move(Transform transform);
        //旋转场馆
        public virtual void RotateGroup(Transform transform) { }
    }
}