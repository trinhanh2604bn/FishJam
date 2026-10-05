using System;

namespace FishPuzzle.Progression
{
    /// <summary>
    /// Runtime score, gold, and lives for the current play session.
    /// This is not a singleton. Services are the only mutators.
    /// </summary>
    public sealed class PlayerProgress
    {
        private PlayerProgress(int score, int gold, int lives)
        {
            Score = score;
            Gold = gold;
            Lives = lives;
        }

        public int Score { get; private set; }

        public int Gold { get; private set; }

        public int Lives { get; private set; }

        public static PlayerProgress CreateDevelopmentDefaults()
        {
            return Create(0, 1000, 5);
        }

        public static PlayerProgress Create(int score, int gold, int lives)
        {
            if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score));
            }

            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gold));
            }

            if (lives < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lives));
            }

            return new PlayerProgress(score, gold, lives);
        }

        internal void AddScore(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Score += amount;
        }

        internal bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            return true;
        }

        internal void AddGold(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Gold += amount;
        }

        internal void DeductLives(int cost)
        {
            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost));
            }

            var next = Lives - cost;
            Lives = next < 0 ? 0 : next;
        }
    }
}
