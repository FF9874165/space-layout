using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    public class SportsBuilding : Building
    {
        public int count;
        public int spectator;
        public bool hasAuxiliary;//是否为场馆？有无大量辅助用房
        public double auxiliaryHeight;//辅助用房高度

        public double reductionRatio;//除球场以外的面积，能额外划分给球场的比例

        public List<double> areaTotalGroupRequired = new List<double>();//该类场馆各单体总面积 理论值
        public List<double> areaTotalGroupActual = new List<double>();//该类场馆各单体总面积 实际值
        public List<double> areaCourtGroupRequired = new List<double>();//该类场馆各单体球场 理论总面积
        public List<double> areaCourtGroupActual = new List<double>();//该类场馆各单体球场 实际总面积
        public List<double> areaAuxiliaryGroupRequired = new List<double>();//该类场馆各单体辅助用房 理论总面积
        public List<double> areaAuxiliaryGroupActual = new List<double>();//该类场馆各单体辅助用房 实际总面积
        public List<double> areaBase = new List<double>();//该类场馆各单体建筑基底面积

        //构建被拆分后的子场馆list
        public virtual void CreateSportsBuildingGroup() { }
    }
}
