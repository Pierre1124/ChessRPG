using UnityEngine;

public static class CardAnimationEvents
{
    public static void Play(
        Piece source,
        Piece target,
        CardDefinition card,
        CardAnimationTiming timing,
        CardEffectData effect = null,
        int amount = 0
    )
    {
        if (card == null)
        {
            return;
        }

        CardAnimationContext context = new CardAnimationContext
        {
            timing = timing,
            card = card,
            effect = effect,
            source = source,
            target = target,
            amount = amount
        };

        Notify(source, context, CardAnimationRecipient.Source);

        if (target != null && target != source)
        {
            Notify(target, context, CardAnimationRecipient.Target);
        }
    }

    private static void Notify(
        Piece piece,
        CardAnimationContext context,
        CardAnimationRecipient recipient
    )
    {
        if (piece == null)
        {
            return;
        }

        context.receiver = piece;
        context.recipient = recipient;

        MonoBehaviour[] behaviours =
            piece.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ICardAnimationReceiver receiver)
            {
                receiver.PlayCardAnimation(context);
            }
        }

        context.receiver = null;
        context.recipient = CardAnimationRecipient.Source;
    }
}
