using System;
using System.Collections.Generic;
using UnityEngine;
namespace Game.Views
{
    public enum MoneySortBoosterType { Shuffle, Hammer, FreeMove }
    public enum MoneySortElementType { Rainbow, HiddenMoney }
    public interface IMoneySortAdAvailability
    {
        event Action RewardedAvailabilityChanged;
        bool IsRewardedAdAvailable(string placement);
    }
    public static class AdsPlacement
    {
        public const string LevelCompleteReward = "level_complete_reward";
        public const string OutOfLivesRv = "out_of_lives_rv";
        public const string OutOfSpace = "out_of_space";
        public const string RentSlot = "rent_slot";
    }
    [Serializable] public sealed class MoneySortTutorialLayout
    {
        public int levelNumber;
        public List<MoneySortTutorialMove> tutorialMoves = new();
        public bool finishWithDeal;
    }
    [Serializable] public struct MoneySortTutorialMove
    {
        public int sourceStackIndex, destinationStackIndex;
    }
}
