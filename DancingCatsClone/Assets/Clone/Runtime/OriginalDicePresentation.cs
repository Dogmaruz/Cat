using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DancingCats.Clone
{
    /// <summary>Original CreatePoint geometry, dice states and half-second Player transitions.</summary>
    public sealed class OriginalDicePresentation : MonoBehaviour
    {
        private OriginalProgression progress;
        private Transform[] spaces;
        private Transform start, player, movingParent;
        private ScrollRect scroll;
        private Animator diceAnimator, playerAnimator;
        private int visiblePosition;
        public bool IsRolling { get; private set; }

        public void Bind(OriginalProgression service, Func<string, Sprite> roleHead, UiLocalization localization)
        {
            progress = service ?? throw new ArgumentNullException(nameof(service));
            if (localization == null) throw new ArgumentNullException(nameof(localization));
            var parent = Require("UIListView_Monopoly/Content/Parent");
            var normal = parent.Find("MonopolyItem");
            var maximum = parent.Find("MaxMonopolyItem");
            var end = parent.Find("EndMonopolyItem");
            var startTemplate = parent.Find("StartMonopolyItem");
            if (normal == null || maximum == null || end == null || startTemplate == null)
                throw new InvalidOperationException("The original dice track templates are missing.");
            start = Clone(startTemplate, parent, "OriginalStart");
            start.localPosition = new Vector3(215, -35, 0);
            spaces = new Transform[progress.Config.diceSpaces.Length];
            for (int index = 0; index < spaces.Length; index++)
            {
                var template = index == spaces.Length - 1 ? end : (index + 1) % 6 == 0 ? maximum : normal;
                var item = Clone(template, parent, "MonopolyItem_" + index);
                item.SetAsFirstSibling(); item.localPosition = NativePosition(index, spaces.Length);
                spaces[index] = item;
                if (index == spaces.Length - 1) continue; // The native end cap has no PlayDiceItem component.
                var reward = progress.Config.diceSpaces[index];
                bool gold = reward.landRweardType == 1 || reward.landRweardType == 5;
                bool role = reward.landRweardType == 2 || reward.landRweardType == 5;
                Set(item, "GoldHead", gold); Set(item, "RoleHead", role); Set(item, "New", reward.landRweardType == 4);
                var text = item.Find("GoldHead/GoldText")?.GetComponent<Text>();
                if (text != null) localization.SetText(text, "X" + reward.goldNumber);
                var head = item.Find("RoleHead")?.GetComponent<Image>();
                if (head != null && role) { head.sprite = roleHead(reward.RoleName); head.preserveAspect = true; }
            }
            normal.gameObject.SetActive(false); maximum.gameObject.SetActive(false);
            end.gameObject.SetActive(false); startTemplate.gameObject.SetActive(false);
            parent.Find("BG_Image")?.SetAsFirstSibling();
            player = Require("Player"); movingParent = Require("RoleParent");
            playerAnimator = player.GetComponent<Animator>();
            diceAnimator = Require("Button_Bg/Shazi").GetComponent<Animator>();
            scroll = Require("UIListView_Monopoly").GetComponent<ScrollRect>();
            if (scroll == null) throw new InvalidOperationException("The original dice ScrollRect is missing.");
            // The native track grows up from the Content bottom pivot. Keep its original geometry.
            if (scroll.viewport == null) scroll.viewport = scroll.transform as RectTransform;
            var viewport = scroll.viewport;
            if (viewport.GetComponent<Mask>() == null && viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
            var graphic = viewport.GetComponent<Graphic>();
            if (graphic == null) { var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear; graphic = hit; }
            graphic.raycastTarget = true;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            Sync();
        }

        public void Sync()
        {
            if (progress == null || IsRolling) return;
            visiblePosition = Mathf.Clamp(progress.State.dicePosition, 0, spaces.Length);
            if (diceAnimator != null) diceAnimator.gameObject.SetActive(false);
            PlacePlayer(visiblePosition); RefreshSpaces(visiblePosition);
        }

        public void BeginRoll(int face)
        {
            if (IsRolling) throw new InvalidOperationException("The original dice is already rolling.");
            visiblePosition = progress.State.dicePosition;
            if (diceAnimator == null)
                throw new InvalidOperationException("The original dice Animator is missing.");
            diceAnimator.gameObject.SetActive(true);
            if (!diceAnimator.isInitialized) { diceAnimator.Rebind(); diceAnimator.Update(0f); }
            if (!diceAnimator.HasState(0, Animator.StringToHash(face.ToString())))
                throw new InvalidOperationException("The original dice face Animator state is missing.");
            IsRolling = true;
            diceAnimator.Play(face.ToString(), 0, 0f);
        }

        public void CancelRoll() { IsRolling = false; Sync(); }
        public void FinishRoll(int destination, Action complete) => StartCoroutine(Move(destination, complete));

        private IEnumerator Move(int destination, Action complete)
        {
            // Native SieveAnimator waits one second before SieveMove starts.
            yield return new WaitForSeconds(1f);
            player.SetParent(movingParent, true);
            while (visiblePosition < Mathf.Min(destination, spaces.Length))
            {
                var point = spaces[visiblePosition];
                var from = player.position;
                if (playerAnimator != null) playerAnimator.Play("Jump", 0, 0f);
                float elapsed = 0;
                while (elapsed < .5f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / .5f);
                    player.position = Vector3.Lerp(from, point.position, 1 - (1 - t) * (1 - t));
                    yield return null;
                }
                visiblePosition++; RefreshSpaces(visiblePosition);
            }
            IsRolling = false; PlacePlayer(visiblePosition);
            diceAnimator.gameObject.SetActive(false);
            complete?.Invoke();
        }

        private void PlacePlayer(int position)
        {
            var target = position == 0 ? start : spaces[Mathf.Min(position, spaces.Length) - 1];
            player.SetParent(target, false); player.localPosition = Vector3.zero; player.localScale = Vector3.one;
            if (playerAnimator != null) playerAnimator.Play("Normal", 0, 0f);
            if (scroll.content != null)
                scroll.content.anchoredPosition = new Vector2(0, position == 0 ? 0 : -target.localPosition.y);
        }

        private void RefreshSpaces(int position)
        {
            for (int index = 0; index < spaces.Length; index++)
            {
                if (index == spaces.Length - 1) continue;
                bool claimed = index < position;
                Set(spaces[index], "BG_Normal", !claimed); Set(spaces[index], "BG_Get", claimed);
                if (claimed) { Set(spaces[index], "GoldHead", false); Set(spaces[index], "RoleHead", false); Set(spaces[index], "New", false); }
            }
        }

        public static Vector3 NativePosition(int index, int count)
        {
            float x = 0, y = 0;
            bool descending = false;
            int along = 0, afterMaximum = 0;
            for (int i = 0; i <= index; i++)
            {
                if (i == count - 1) { y += 150; break; }
                float extra = 0;
                if ((i + 1) % 6 == 0) { extra = 40; afterMaximum = i + 2; }
                if (i + 1 == afterMaximum) extra = 40;
                if (i == 0) { x = 20; y = 70; continue; }
                if (i <= 3) { x -= 145; y += 80; continue; }
                if (descending)
                {
                    if (along > 4) { x += 145 + extra; descending = false; along = 1; }
                    else { x -= 145 + extra; along++; }
                }
                else
                {
                    if (along > 4) { x -= 145 + extra; descending = true; along = 1; }
                    else { x += 145 + extra; along++; }
                }
                y += 80;
            }
            return new Vector3(x, y, 0);
        }

        private static Transform Clone(Transform source, Transform parent, string name)
        {
            var copy = Instantiate(source, parent, false); copy.name = name;
            copy.localScale = Vector3.one; copy.gameObject.SetActive(true);
            return copy;
        }
        private Transform Require(string path) => transform.Find(path) ?? throw new InvalidOperationException("Original dice hierarchy is missing: " + path);
        private static void Set(Transform root, string path, bool active) { var target = root.Find(path); if (target != null) target.gameObject.SetActive(active); }
        private void OnEnable() { if (progress != null) Sync(); }
        private void OnDisable() { IsRolling = false; }
    }
}
