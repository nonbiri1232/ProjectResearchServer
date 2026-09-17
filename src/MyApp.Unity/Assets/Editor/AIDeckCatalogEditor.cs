using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIDeckCatalog))]
public class AIDeckCatalogEditor : Editor
{
    private SerializedProperty decks;

    private void OnEnable()
    {
        decks = serializedObject.FindProperty("decks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            $"各デッキは合計{DeckManager.MAXDECKNUM}枚、同じカードは最大" +
            $"{DeckManager.MAXSAMECARD}枚です。",
            MessageType.Info);

        for(int deckIndex = 0; deckIndex < decks.arraySize; deckIndex++)
        {
            SerializedProperty deck = decks.GetArrayElementAtIndex(deckIndex);
            SerializedProperty deckId = deck.FindPropertyRelative("deckId");
            SerializedProperty cardAmounts = deck.FindPropertyRelative("cardAmounts");
            int total = GetTotal(cardAmounts);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            deck.isExpanded = EditorGUILayout.Foldout(
                deck.isExpanded,
                $"Deck ID {deckId.intValue}  ({total}/{DeckManager.MAXDECKNUM}枚)",
                true);
            if(GUILayout.Button("削除", GUILayout.Width(50)))
            {
                decks.DeleteArrayElementAtIndex(deckIndex);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if(deck.isExpanded)
            {
                EditorGUILayout.PropertyField(deckId, new GUIContent("Deck ID"));
                DrawCardAmounts(cardAmounts);

                MessageType messageType = total == DeckManager.MAXDECKNUM
                    ? MessageType.Info
                    : MessageType.Warning;
                EditorGUILayout.HelpBox(
                    $"合計枚数: {total}/{DeckManager.MAXDECKNUM}", messageType);
            }
            EditorGUILayout.EndVertical();
        }

        if(GUILayout.Button("デッキを追加"))
        {
            int index = decks.arraySize;
            decks.InsertArrayElementAtIndex(index);
            SerializedProperty deck = decks.GetArrayElementAtIndex(index);
            deck.FindPropertyRelative("deckId").intValue = GetNextDeckId();
            InitializeCardAmounts(deck.FindPropertyRelative("cardAmounts"));
            deck.FindPropertyRelative("cardIds").ClearArray();
            deck.isExpanded = true;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawCardAmounts(SerializedProperty cardAmounts)
    {
        EnsureCardAmounts(cardAmounts);
        for(int i = 0; i < cardAmounts.arraySize; i++)
        {
            SerializedProperty item = cardAmounts.GetArrayElementAtIndex(i);
            int cardId = item.FindPropertyRelative("cardId").intValue;
            SerializedProperty amount = item.FindPropertyRelative("amount");
            string cardName = Card.GetCardClassName(cardId) ?? "Unknown";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID {cardId}: {cardName}");
            amount.intValue = EditorGUILayout.IntSlider(
                amount.intValue, 0, DeckManager.MAXSAMECARD, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();
        }
    }

    private static int GetTotal(SerializedProperty cardAmounts)
    {
        int total = 0;
        for(int i = 0; i < cardAmounts.arraySize; i++)
        {
            total += cardAmounts.GetArrayElementAtIndex(i)
                .FindPropertyRelative("amount").intValue;
        }
        return total;
    }

    private int GetNextDeckId()
    {
        int highestId = -1;
        for(int i = 0; i < decks.arraySize; i++)
        {
            highestId = Mathf.Max(highestId,
                decks.GetArrayElementAtIndex(i).FindPropertyRelative("deckId").intValue);
        }
        return highestId + 1;
    }

    private static void EnsureCardAmounts(SerializedProperty cardAmounts)
    {
        int expectedCount = AIDeckCatalog.LastCardId - AIDeckCatalog.FirstCardId + 1;
        if(cardAmounts.arraySize == expectedCount)return;
        InitializeCardAmounts(cardAmounts);
    }

    private static void InitializeCardAmounts(SerializedProperty cardAmounts)
    {
        cardAmounts.ClearArray();
        for(int cardId = AIDeckCatalog.FirstCardId;
            cardId <= AIDeckCatalog.LastCardId; cardId++)
        {
            int index = cardAmounts.arraySize;
            cardAmounts.InsertArrayElementAtIndex(index);
            SerializedProperty item = cardAmounts.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("cardId").intValue = cardId;
            item.FindPropertyRelative("amount").intValue = 0;
        }
    }
}
