using System;
using System.Globalization;
using System.Linq;
namespace Game.Views
{
    public static class MoneySortUiValue
    {
        public static int Number(string value) => int.TryParse(new string((value??string.Empty).Where(char.IsDigit).ToArray()),NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)?n:0;
        public static MoneySortBoosterType Booster(int index)=>index==0?MoneySortBoosterType.FreeMove:index==1?MoneySortBoosterType.Shuffle:MoneySortBoosterType.Hammer;
        public static int BoosterIndex(MoneySortBoosterType type)=>type==MoneySortBoosterType.FreeMove?0:type==MoneySortBoosterType.Shuffle?1:2;
    }
}
