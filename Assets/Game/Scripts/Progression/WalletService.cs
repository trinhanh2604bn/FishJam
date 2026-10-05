namespace FishPuzzle.Progression
{
    /// <summary>
    /// The only gold mutator. UI reads the wallet result and does not change gold itself.
    /// </summary>
    public sealed class WalletService
    {
        public bool TrySpend(PlayerProgress progress, int amount)
        {
            if (progress == null || amount < 0)
            {
                return false;
            }

            return progress.TrySpendGold(amount);
        }

        public void Refund(PlayerProgress progress, int amount)
        {
            if (progress == null || amount <= 0)
            {
                return;
            }

            progress.AddGold(amount);
        }
    }
}
