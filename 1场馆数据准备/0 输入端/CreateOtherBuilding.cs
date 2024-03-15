using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    public class CreateOtherBuilding : GH_Component
    {
        public CreateOtherBuilding()
          : base("创建其他功能", "其他功能",
              "初始化其他功能信息，生成初始排布",
              "建筑空间布局", "数据预处理")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("是否设置此馆", "是否设置", "本项目是否包含此馆", GH_ParamAccess.item);
            pManager.AddNumberParameter("场馆面积", "面积", "此类场地合计面积", GH_ParamAccess.item);
            pManager.AddPointParameter("陈列基点", "基点", "此类球场陈列基点", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBoxParameter("场馆边界", "场馆边界", "场馆单体的空间范围", GH_ParamAccess.list);
            pManager.AddBoxParameter("各层单元", "单元", "各层单元独立为一个对象", GH_ParamAccess.list);
            pManager.AddGenericParameter("建筑场馆", "场馆", "本项目中包括的建筑场馆类型", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //注册函数到重新生成的事件+
            Manager.restartEvent += Restart;

            //数据初始化
            bool IsValid = false;
            double area = 0;
            Point3d origin = new Point3d();

            if (!DA.GetData(0, ref IsValid)) return;
            if (!DA.GetData(1, ref area)) return;
            if (!DA.GetData(2, ref origin)) return;


            if (IsValid)
            {
                //新建办公场馆
                OtherFunction otherFunction = new OtherFunction(area, origin);
                //获取办公建筑边界
                GH_Box buildingBoundary = otherFunction.groupBoundary;
                //获取办公单元
                List<GH_Box> officeUnit = new List<GH_Box>();
                for (int i = 0; i < otherFunction.unitCount; i++)
                {
                    officeUnit.Add(otherFunction.functionUnits[i].functionUnit);
                }

                DA.SetData(0, buildingBoundary);
                DA.SetDataList(1, officeUnit);
                DA.SetData(2, otherFunction);
            }
        }
        //Manager监听的方法，当本轮生成结束后调用
        public void Restart()
        {
            OnPingDocument().ScheduleSolution(StaticObject.timeSpan, new GH_Document.GH_ScheduleDelegate(this.ScheduleCallback));
        }
        //重启下一轮计算的方法
        private void ScheduleCallback(GH_Document doc)
        {
            ExpireSolution(false);
        }
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("AFB9BAD8-08AE-45CF-9B4A-45C06C1FEB75"); }
        }
    }
}