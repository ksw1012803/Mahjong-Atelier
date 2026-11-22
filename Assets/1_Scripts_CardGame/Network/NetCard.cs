using Mirror;
using CardGame.Core;

namespace CardGame.Network
{
    /// <summary>
    /// 네트워크로 전송되는 카드 식별자 (4바이트).
    ///   defIndex   : CardDatabase.OrderedDefinitions 인덱스 (어떤 종류의 카드인지)
    ///   instanceId : Deck.CreateDeck 시 부여된 고유 번호 (같은 종류 2장 중 어느 것인지)
    /// instanceId &lt; 0 이면 "숨김/없음" (상대 카드).
    /// </summary>
    public struct NetCard
    {
        public short defIndex;
        public short instanceId;

        public static readonly NetCard None = new NetCard { defIndex = -1, instanceId = -1 };
        public bool IsValid => instanceId >= 0 && defIndex >= 0;

        public static NetCard From(CardGameManager gm, CardData card)
        {
            if (gm == null || card == null || card.IsPlaceholder) return None;
            int def = gm.GetDefIndex(card);
            if (def < 0 || card.InstanceId < 0) return None;
            return new NetCard { defIndex = (short)def, instanceId = (short)card.InstanceId };
        }

        public CardData Resolve(CardGameManager gm)
        {
            if (!IsValid || gm == null) return null;
            return gm.ResolveCard(defIndex, instanceId);
        }

        public override string ToString() => IsValid ? $"NetCard(def={defIndex}, id={instanceId})" : "NetCard(None)";
    }

    /// <summary>Mirror 직렬화 확장 — Weaver가 자동 인식 (NetCard, NetCard[], List&lt;NetCard&gt;).</summary>
    public static class NetCardReaderWriter
    {
        public static void WriteNetCard(this NetworkWriter writer, NetCard value)
        {
            writer.WriteShort(value.defIndex);
            writer.WriteShort(value.instanceId);
        }

        public static NetCard ReadNetCard(this NetworkReader reader)
        {
            return new NetCard
            {
                defIndex = reader.ReadShort(),
                instanceId = reader.ReadShort()
            };
        }
    }
}
