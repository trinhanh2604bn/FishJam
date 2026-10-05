using System;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// One fish during a level attempt. Identity is the runtime id plus <see cref="FishType"/>.
    /// </summary>
    public sealed class FishRuntimeState
    {
        public FishRuntimeState(int id, FishType type, string sourceBubbleId)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Fish runtime ids start at 1.");
            }

            Id = id;
            Type = type;
            SourceBubbleId = sourceBubbleId ?? string.Empty;
            State = FishState.Idle;
        }

        public int Id { get; }

        public FishType Type { get; }

        public string SourceBubbleId { get; }

        public FishState State { get; private set; }

        public void Reserve()
        {
            Require(FishState.Idle, nameof(Reserve));
            State = FishState.Reserved;
        }

        public void BeginTransit()
        {
            Require(FishState.Reserved, nameof(BeginTransit));
            State = FishState.InTransit;
        }

        public void CommitToTank()
        {
            Require(FishState.InTransit, nameof(CommitToTank));
            State = FishState.Tank;
        }

        public void CommitToWaitingTray()
        {
            Require(FishState.InTransit, nameof(CommitToWaitingTray));
            State = FishState.WaitingTray;
        }

        public void BeginTransitFromWaitingTray()
        {
            Require(FishState.WaitingTray, nameof(BeginTransitFromWaitingTray));
            State = FishState.InTransit;
        }

        public void Consume()
        {
            Require(FishState.Tank, nameof(Consume));
            State = FishState.Consumed;
        }

        private void Require(FishState expected, string operation)
        {
            if (State == expected)
            {
                return;
            }

            throw new InvalidOperationException(
                "Fish " + Id + " cannot " + operation + " from " + State + ". Expected " + expected + ".");
        }
    }
}
