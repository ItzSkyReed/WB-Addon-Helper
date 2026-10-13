namespace BacapGenerator.Advancements.Models;

public static class BacapAdvancementTierExtension
{
    extension(BacapAdvancementTier tier)
    {
        public string MinecraftColor() =>
            tier switch
            {
                BacapAdvancementTier.Task => "#green",
                BacapAdvancementTier.Goal => "#75E1FF",
                BacapAdvancementTier.Challenge => "dark_purple",
                BacapAdvancementTier.SuperChallenge => "#FF2A2A",
                BacapAdvancementTier.Root => "#CCCCCC",
                BacapAdvancementTier.Milestone => "yellow",
                BacapAdvancementTier.AdvancementLegend => "gold",
                BacapAdvancementTier.Hidden => "light_purple",

                _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
            };

        public string Color() =>
            tier switch
            {
                BacapAdvancementTier.Task => "#55FF55",
                BacapAdvancementTier.Goal => "#75E1FF",
                BacapAdvancementTier.Challenge => "#AA00AA",
                BacapAdvancementTier.SuperChallenge => "#FF2A2A",
                BacapAdvancementTier.Root => "#CCCCCC",
                BacapAdvancementTier.Milestone => "#FFFF55",
                BacapAdvancementTier.AdvancementLegend => "#FFAA00",
                BacapAdvancementTier.Hidden => "#FF55FF",

                _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
            };

        public string TechnicalName() =>
            tier switch
            {
                BacapAdvancementTier.Task => "task",
                BacapAdvancementTier.Goal => "goal",
                BacapAdvancementTier.Challenge => "challenge",
                BacapAdvancementTier.SuperChallenge => "super_challenge",
                BacapAdvancementTier.Root => "root",
                BacapAdvancementTier.Milestone => "milestone",
                BacapAdvancementTier.AdvancementLegend => "advancement_legend",
                BacapAdvancementTier.Hidden => "hidden",

                _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
            };

        public string DisplayName() =>
            tier switch
            {
                BacapAdvancementTier.Task => "Task",
                BacapAdvancementTier.Goal => "Goal",
                BacapAdvancementTier.Challenge => "Challenge",
                BacapAdvancementTier.SuperChallenge => "Super Challenge",
                BacapAdvancementTier.Root => "Root",
                BacapAdvancementTier.Milestone => "Milestone",
                BacapAdvancementTier.AdvancementLegend => "Advancement Legend",
                BacapAdvancementTier.Hidden => "Hidden",

                _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
            };
    }
}