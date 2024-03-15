using System;
using System.Collections.Generic;
using Space_Layout;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Types;

namespace Space_Layout
{
    public class CreateLobby : GH_Component
    {
        public CreateLobby()
          : base("创建大厅群组", "共享大厅",
              "创建本项目中负责管理大厅的对象",
             "建筑空间布局", "数据预处理")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("大厅面积", "场馆总建筑面积", "大厅功能的总面积", GH_ParamAccess.item);
            pManager.AddNumberParameter("大厅最小面积", "单一场馆最小建筑面积", "大厅单体的最小面积", GH_ParamAccess.item);
            pManager.AddPointParameter("陈列基点", "展示基点", "大厅陈列基点", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("大厅类", "共享大厅", "本项目中负责管理大厅的对象", GH_ParamAccess.item);
            pManager.AddBoxParameter("场馆边界", "大厅单体", "场馆单体的空间范围", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //注册函数到重新生成的事件+
            Manager.restartEvent += Restart;

            //输入外部数据
            double area = 0;
            double minArea = 0;
            Point3d origin = new Point3d();

            if (!DA.GetData(0, ref area)) return;
            if (!DA.GetData(1, ref minArea)) return;
            if (!DA.GetData(2, ref origin)) return;

            //创建大厅实体
            Lobby lobby = new Lobby(area, minArea, origin);
            List<GH_Box> lobbyUnits = new List<GH_Box>();
            lobby.ShowLayout();

            //输出数据
            for (int i = 0; i < lobby.count; i++)
            {
                lobbyUnits.Add(lobby.lobbyUnits[i].groupBoundary);
            }
            DA.SetData(0, lobby);
            DA.SetDataList(1, lobbyUnits);
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
        protected override System.Drawing.Bitmap Icon => Properties.Resources._3_大厅;

        public override Guid ComponentGuid
        {
            get { return new Guid("43190819-4284-49D2-8C25-734EF1DFD371"); }
        }
    }
}