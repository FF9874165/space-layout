using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //运动场地，用作父类
    public class Court
    {
        public CourtType courtType;
        //单个场地信息
        public double lengthPerCourt;//场地长度
        public double widthPerCourt;//场地宽度
        public double clearHeight;//场地净高
        //算场地数量涉及的信息
        public double terminalDistance;//端线距离
        public double sidelineDistance;//边线距离
        public double terminalSpacing;//端线间距
        public double sidelineSpacing;//边线间距
        //观众坐席
        public double lineSpacing = 0.8;//观众坐席排距，有背硬椅，体育规范13页
        public double seatWidth = 0.5;//观众坐席座椅宽度，有背硬椅0.48，体育规范13页，为便于计算，标准稍提高
        public double mainEvacuationWidth = 1.1;//观众坐席双侧座椅疏散纵走道宽度，体育规范15页
        public double secondaryEvacuationWidth = 0.9;//观众坐席单侧座椅疏散纵走道宽度，体育规范15页
    }
}
