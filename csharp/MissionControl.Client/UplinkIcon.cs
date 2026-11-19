using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;

namespace MissionControl.Client;

internal static class UplinkIcon
{
    internal const string TemplateId = "a0b1c2d3e4f5000000000001";
    private static Texture2D iconTexture;
    private static readonly Dictionary<int, Sprite> Sprites = new();

    internal static void Load()
    {
        using var stream = typeof(UplinkIcon).Assembly.GetManifestResourceStream("MissionControl.Uplink.png");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, buffer.ToArray(), markNonReadable: true))
        {
            UnityEngine.Object.Destroy(texture);
            throw new InvalidDataException("Could not decode the mission uplink icon.");
        }
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        iconTexture = texture;
        GetSprite(1);
    }

    private static Sprite GetSprite(int scaleFactor)
    {
        int scale = Math.Max(1, scaleFactor);
        if (Sprites.TryGetValue(scale, out var sprite)) return sprite;

        // EFT calls Image.SetNativeSize in the deal panel. Keep the high-resolution artwork
        // but give it the same UI dimensions as a game-generated one-cell icon at this scale.
        int cellPixels = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1)).X;
        float pixelsPerUnit = 100f * iconTexture.width / cellPixels / scale;
        sprite = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height),
            new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Sprites.Add(scale, sprite);
        return sprite;
    }

    [HarmonyPatch(typeof(ItemViewFactory), nameof(ItemViewFactory.LoadItemIcon))]
    private static class ItemViewPatch
    {
        private static bool Prefix(Item item, int scaleFactor, ref ItemIcon __result)
        {
            if (item?.TemplateId.ToString() != TemplateId || iconTexture == null) return true;
            __result = new ItemIcon(IconsHash.Default) { Sprite = GetSprite(scaleFactor) };
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemViewFactory), nameof(ItemViewFactory.GetItemSpriteAsync))]
    private static class SpritePatch
    {
        private static bool Prefix(Item item, int scaleFactor, ref Task<Sprite> __result)
        {
            if (item?.TemplateId.ToString() != TemplateId || iconTexture == null) return true;
            __result = Task.FromResult(GetSprite(scaleFactor));
            return false;
        }
    }
}
