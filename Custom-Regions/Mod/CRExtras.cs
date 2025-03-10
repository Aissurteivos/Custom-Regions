﻿using System;
using UnityEngine;
using System.Threading;
using System.IO;
using System.Reflection;
using MonoMod.RuntimeDetour;
using System.Collections.Generic;
using System.Linq;
using RWCustom;

namespace CustomRegions.Mod
{

    public static class CRExtras
    {
        public enum CustomFolder
        {
            // Depth 0
            None,
            // Depth 1
            Assets,
            World,
            Levels,
            PackDependencies,
            // Depth 2.1
            Gates_Shelters,
            Gates,
            Regions,
            // Depth 2.2
            Futile,
            Text,
            // Depth 3
            RegionID,
            // Depth 4.1
            Rooms,
            // Depth 4.2
            Resources,
            // Depth 5
            Illustrations,
            LoadedSoundEffects,
            Scenes,
            Atlases,
            Music,
            Decals,
            Palettes,
            // Depth 6
            ///<summary>LoadedSoundEffects/Ambient!!!!</summary>
            Ambient,
            Procedural,
            Songs

        }

        public static void DisableTogglePack(string packName)
        {
            // Disable

            CustomWorldStructs.RegionPack pack = CustomWorldMod.installedPacks[packName];
            pack.activated = !pack.activated;
            CustomWorldMod.SerializePackInfoJSON(CRExtras.BuildPath(pack.folderName, CRExtras.CustomFolder.None, file: "packInfo.json"), pack);
            CustomWorldMod.LoadCustomWorldResources();
        }


        public static CustomWorldStructs.ProcessedThumbnail ProccessThumbnail(Texture2D oldTex, byte[] data, string packName)//,  bool activated, bool raindb)
        {
            Color colorEdge;
            Texture2D newTex = new Texture2D(oldTex.width, oldTex.height, TextureFormat.RGBA32, false);
            Color[] convertedImage = oldTex.GetPixels();
            List<HSLColor> hslColors = new List<HSLColor>();
            int numberOfPixels = convertedImage.Length;
            for (int c = 0; c < numberOfPixels; c++)
            {
                /*
                // Change opacity if not active
                if (!activated && !raindb)
                {
                    convertedImage[c].a *= 0.65f;
                }
                */
                HSLColor hslColor = CRExtras.RGB2HSL(convertedImage[c]);
                if (hslColor.saturation > 0.25 && hslColor.lightness > 0.25 && hslColor.lightness < 0.75f)
                {
                    hslColors.Add(hslColor);
                }
            }
            float averageLight = 0f;
            float averageSat = 0f;
            float medianHue = 0f;

            // Calculate average light and sat
            if (hslColors.Count > 0)
            {
                foreach (HSLColor color in hslColors)
                {
                    averageLight += color.lightness / hslColors.Count;
                    averageSat += color.saturation / hslColors.Count;
                }
            }
            // Calculate median hue
            int half = hslColors.Count() / 2;
            var sortedColors = hslColors.OrderBy(x => x.hue);
            if (half != 0 && half < sortedColors.Count())
            {
                try
                {
                    if ((hslColors.Count % 2) == 0)
                    {
                        medianHue = (sortedColors.ElementAt(half).hue + sortedColors.ElementAt(half - 1).hue) / 2;
                    }
                    else
                    {
                        medianHue = sortedColors.ElementAt(half).hue;
                    }
                }
                catch (Exception e) { CustomWorldMod.Log($"Cannot calculate median hue [{e}] for [{packName}]", true); }
            }

            //if ((activated || raindb))
            {
                if (averageSat > 0.15f)
                {
                    colorEdge = Color.Lerp(Custom.HSL2RGB(medianHue, averageSat, Mathf.Lerp(averageLight, 0.6f, 0.5f)), Color.white, 0.175f);
                }
                else
                {
                    colorEdge = Custom.HSL2RGB(UnityEngine.Random.Range(0.1f, 0.75f), 0.4f, 0.75f);
                }
                CustomWorldMod.Log($"Color for [{packName}] - MedianHue [{medianHue}] averageSat [{averageSat}] averagelight [{averageLight}] " +
                    $"- Number of pixels [{numberOfPixels}]]", false, CustomWorldMod.DebugLevel.FULL);
            }

            hslColors.Clear();

            //newTex.SetPixels(convertedImage);
            //newTex.Apply();
            //TextureScale.Point(newTex, (int)(thumbSize.x), (int)(thumbSize.y));
            CustomWorldStructs.ProcessedThumbnail procThumb = new CustomWorldStructs.ProcessedThumbnail();

            procThumb.dateDownloaded = DateTime.UtcNow;
            procThumb.mainColor = colorEdge;
            procThumb.data = data;

            CustomWorldMod.Log($"Processed thumbnail for [{packName}] at [{procThumb.dateDownloaded}]", false, CustomWorldMod.DebugLevel.MEDIUM);

            return procThumb;
        }


        /// <summary>
        /// Builds a folder path. It will return a specific file if specified, otherwise it will end with backslash.
        /// </summary>
        /// <param name="regionPackFolder"> Folder name of the region pack. Use null for vanilla path. </param>
        /// <param name="folderEnum"> Folder which to retreive.</param>
        /// <param name="regionID"> Region ID needed for Rooms or RegionID folder.</param>
        /// <param name="file"> If specified, it will append a file after the folder path.</param>
        /// <param name="folder"> If specified, it will append an additional folder path.</param>
        /// <returns>Path built.</returns>
        public static string BuildPath(string regionPackFolder, CustomFolder folderEnum, string regionID = null,
            string file = null, string folder = null, bool includeRoot = true, bool recursive = false)
        {
            char div = Path.DirectorySeparatorChar;

            if (folderEnum == CustomFolder.RegionID || folderEnum == CustomFolder.Rooms)
            {
                if (string.IsNullOrEmpty(regionID))
                {
                    CustomWorldMod.Log($"Called BuildPath without regionID", true);
                    return null;
                }
            }
            string path = string.Empty;
            string recursivePath = string.Empty;
            switch (folderEnum)
            {
                // Depth 1
                case CustomFolder.None:
                case CustomFolder.Assets:
                case CustomFolder.Levels:
                case CustomFolder.World:
                case CustomFolder.PackDependencies:
                    if (includeRoot)
                    {
                        // Rain World/
                        path = RWCustom.Custom.RootFolderDirectory() + path;
                    }

                    // Requesting custom folder
                    if (regionPackFolder != null)
                    {
                        // includeRoot?/Mods/CustomResources/RegionPack
                        path = path + CustomWorldMod.resourcePath + regionPackFolder;
                    }
                    if (folderEnum != CustomFolder.None)
                    {
                        path = path + div + folderEnum.ToString();
                    }
                    break;

                // Depth 2
                case CustomFolder.Gates_Shelters:
                case CustomFolder.Gates:
                case CustomFolder.Regions:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.World, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + folderEnum.ToString().Replace("_", " ");
                    break;
                case CustomFolder.Futile:
                case CustomFolder.Text:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.Assets, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + folderEnum.ToString();
                    break;

                // Special case
                case CustomFolder.RegionID:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.Regions, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + regionID;
                    break;

                case CustomFolder.Rooms:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.RegionID, regionID: regionID, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + folderEnum.ToString();
                    break;

                // Depth 3
                case CustomFolder.Resources:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.Futile, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + folderEnum.ToString();
                    break;

                //  Depth 4
                case CustomFolder.Illustrations:
                case CustomFolder.LoadedSoundEffects:
                case CustomFolder.Scenes:
                case CustomFolder.Atlases:
                case CustomFolder.Music:
                case CustomFolder.Decals:
                case CustomFolder.Palettes:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.Resources, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + folderEnum.ToString();
                    break;

                // Depth 5.1
                case CustomFolder.Ambient:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.LoadedSoundEffects, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + folderEnum.ToString();
                    break;
                // Depth 5.2
                case CustomFolder.Procedural:
                case CustomFolder.Songs:
                    recursivePath = BuildPath(regionPackFolder, CustomFolder.Music, includeRoot: includeRoot, recursive: true);
                    path = recursivePath + div + folderEnum.ToString();
                    break;

                default:
                    path = null;
                    break;
            }

            if (path == null)
            {
                CustomWorldMod.Log($"[PathBuilder] Could not find request folder [{folderEnum}]", true);
                return string.Empty;
            }

            if (!recursive)
            {
                path += folder != null ? (div.ToString() + folder + div.ToString()) : div.ToString();
                path += file != null ? file : string.Empty;
            }

            return path;
        }


        // Source: https://www.programmingalgorithms.com/algorithm/rgb-to-hsl/
        public static HSLColor RGB2HSL(Color color)
        {
            HSLColor hsl;

            float r = color.r;
            float g = color.g;
            float b = color.b;

            float min = Math.Min(Math.Min(r, g), b);
            float max = Math.Max(Math.Max(r, g), b);
            float delta = max - min;

            hsl.lightness = (max + min) / 2;

            if (delta == 0)
            {
                hsl.hue = 0;
                hsl.saturation = 0.0f;
            }
            else
            {
                hsl.saturation = (hsl.lightness <= 0.5) ? (delta / (max + min)) : (delta / (2 - max - min));

                float hue;

                if (r == max)
                {
                    hue = ((g - b) / 6) / delta;
                }
                else if (g == max)
                {
                    hue = (1.0f / 3) + ((b - r) / 6) / delta;
                }
                else
                {
                    hue = (2.0f / 3) + ((r - g) / 6) / delta;
                }

                if (hue < 0)
                    hue += 1;
                if (hue > 1)
                    hue -= 1;

                //hsl.hue = (hue * 360);
                hsl.hue = hue;
            }

            //hsl.saturation *= 1 / 360f;
            //hsl.lightness *= 1 / 360f;
            //hsl.hue *= 1 / 360f;
            return hsl;

        }

        public static void TrimString(ref string reference, float targetPixel, string endSequence)
        {
            if (targetPixel < 0)
            {
                return;
            }
            if (OptionalUI.LabelTest.GetWidth(reference, false) > targetPixel)
            {
                reference = reference.Remove(reference.Length - 1);
                TrimString(ref reference, targetPixel, endSequence);
            }
        }


        public static void TryPlayMenuSound(SoundID soundID)
        {
            try
            {
                (CustomWorldMod.rainWorldInstance.processManager.currentMainLoop as Menu.Menu).PlaySound(soundID);
            }
            catch (Exception e) { CustomWorldMod.Log("Exception " + e, false); }
        }
    }

    /// <summary>
    /// It uses a normal hook if you have 0 or 1 patch mods, but implements its own hook handler using a NativeDetour if you have more
    /// Author: Slime_Cubed
    /// </summary>
    public static class APOFSFix
    {
        public static EventInfo redirectHooks;

        private static MethodInfo apofs = apofs = typeof(SaveState).GetMethod("AbstractPhysicalObjectFromString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        public static event On.SaveState.hook_AbstractPhysicalObjectFromString On_SaveState_AbstractPhysicalObjectFromString
        {
            add
            {
                if (CountPatches() >= 2)
                {
                    if (redirectHooks == null)
                    {
                        Debug.Log($"{nameof(APOFSFix)} using custom hook manager from {Assembly.GetExecutingAssembly().FullName}");
                        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            try
                            {
                                Type t = asm.GetType(nameof(APOFSFix), false);
                                if (t == null) continue;
                                FieldInfo rh = t.GetField(nameof(redirectHooks));
                                if (rh == null) continue;
                                rh.SetValue(null, typeof(APOFSFix).GetEvent(nameof(On_SaveState_AbstractPhysicalObjectFromString)));
                            }
                            catch (Exception) { }
                        }
                    }

                    if (redirectHooks.DeclaringType != typeof(APOFSFix))
                        redirectHooks.AddEventHandler(null, value);

                    if (apofsDetour == null)
                    {
                        Debug.Log("Creating native detour for SaveState.AbstractPhysicalObjectFromString...");
                        apofsDetour = new NativeDetour(apofs, typeof(APOFSFix).GetMethod(nameof(Hook_AbstractPhysicalObjectFromString)));
                    }

                    APOFSHook hk = new APOFSHook(value);
                    if (baseApofsHook != null)
                        baseApofsHook.last = hk;
                    hk.next = baseApofsHook;
                    baseApofsHook = hk;
                }
                else
                {
                    // A plain hook can be used when no issue is found
                    On.SaveState.AbstractPhysicalObjectFromString += value;
                }
            }
            remove
            {
                if (CountPatches() >= 2)
                {
                    if (redirectHooks.DeclaringType != typeof(APOFSFix))
                        redirectHooks.RemoveEventHandler(null, value);

                    APOFSHook hk = baseApofsHook;
                    while (hk != null)
                    {
                        if (hk.target == value)
                        {
                            hk.Remove();
                            break;
                        }
                        hk = hk.next;
                    }
                }
                else
                {
                    On.SaveState.AbstractPhysicalObjectFromString -= value;
                }
            }
        }

        // Act as a detour manager
        private static NativeDetour apofsDetour;
        private static APOFSHook baseApofsHook;

        public static AbstractPhysicalObject Hook_AbstractPhysicalObjectFromString(World world, string str)
        {
            if (baseApofsHook == null) return Orig_AbstractPhysicalObjectFromString(world, str);

            return baseApofsHook.Invoke(world, str);
        }

        public static AbstractPhysicalObject Orig_AbstractPhysicalObjectFromString(World world, string str)
        {
            // Generating a trampoline may cause a segfault
            apofsDetour.Undo();
            AbstractPhysicalObject ret = SaveState.AbstractPhysicalObjectFromString(world, str);
            apofsDetour.Apply();
            return ret;
        }

        private class APOFSHook
        {
            public On.SaveState.hook_AbstractPhysicalObjectFromString target;
            public APOFSHook next;
            public APOFSHook last;

            public APOFSHook(On.SaveState.hook_AbstractPhysicalObjectFromString target)
            {
                this.target = target;
            }

            public AbstractPhysicalObject Invoke(World world, string str)
            {
                // Find the next function down the chain
                On.SaveState.orig_AbstractPhysicalObjectFromString orig;
                if (next == null) orig = Orig_AbstractPhysicalObjectFromString;
                else orig = next.Invoke;

                // Invoke
                return target(orig, world, str);
            }

            public void Remove()
            {
                if (last == null)
                    baseApofsHook = next;
                else
                    last.next = next;

                if (next != null)
                    next.last = last;
            }
        }

        public static int patchesApplied = -1;
        public static int CountPatches()
        {
            if (patchesApplied >= 0) return patchesApplied;

            // Check if any other assemblies have performed this check
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly asm in asms)
            {
                // Don't throw
                try
                {
                    Type t = asm.GetType(nameof(APOFSFix), false);
                    if (t == null) continue;
                    FieldInfo pa = t.GetField(nameof(patchesApplied), BindingFlags.Public | BindingFlags.Static);
                    if (pa.GetValue(null) is int otherPatchesApplied && otherPatchesApplied >= 0)
                        return patchesApplied = otherPatchesApplied;
                }
                catch (Exception) { }
            }

            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // This bug only happens in Partiality
            if (Path.GetFileName(dir) != "Mods")
                return patchesApplied = 0;

            // Count the number of patches enabled
            patchesApplied = 0;
            string[] metas = Directory.GetFiles(dir, "*.modMeta");

            for (int i = 0; i < metas.Length; i++)
            {
                bool patch = false;
                bool enabled = false;
                string[] lines = File.ReadAllLines(Path.Combine(dir, metas[i]));
                // Check whether this mod is a patch and is enabled
                for (int line = 0; line < lines.Length; line++)
                {
                    if (lines[line] == "isEnabled: true") enabled = true;
                    else if (lines[line] == "isPatch: true") patch = true;
                    else continue;
                    if (enabled && patch)
                    {
                        patchesApplied++;
                        break;
                    }
                }
            }

            return patchesApplied;
        }
    }

    // SOURCE: http://wiki.unity3d.com/index.php/TextureScale#Usage
    // AUTHOR: Eric Haines (Eric5h5)
    // Only works on ARGB32, RGB24 and Alpha8 textures that are marked readable
    public class TextureScale
    {
        public class ThreadData
        {
            public int start;
            public int end;
            public ThreadData(int s, int e)
            {
                start = s;
                end = e;
            }
        }

        private static Color[] texColors;
        private static Color[] newColors;
        private static int w;
        private static float ratioX;
        private static float ratioY;
        private static int w2;
        private static int finishCount;
        private static Mutex mutex;

        public static void Point(Texture2D tex, int newWidth, int newHeight)
        {
            ThreadedScale(tex, newWidth, newHeight, false);
        }

        public static void Bilinear(Texture2D tex, int newWidth, int newHeight)
        {
            ThreadedScale(tex, newWidth, newHeight, true);
        }

        private static void ThreadedScale(Texture2D tex, int newWidth, int newHeight, bool useBilinear)
        {
            texColors = tex.GetPixels();
            newColors = new Color[newWidth * newHeight];
            if (useBilinear)
            {
                ratioX = 1.0f / ((float)newWidth / (tex.width - 1));
                ratioY = 1.0f / ((float)newHeight / (tex.height - 1));
            }
            else
            {
                ratioX = ((float)tex.width) / newWidth;
                ratioY = ((float)tex.height) / newHeight;
            }
            w = tex.width;
            w2 = newWidth;
            var cores = Mathf.Min(SystemInfo.processorCount, newHeight);
            var slice = newHeight / cores;

            finishCount = 0;
            if (mutex == null)
            {
                mutex = new Mutex(false);
            }
            if (cores > 1)
            {
                int i = 0;
                ThreadData threadData;
                for (i = 0; i < cores - 1; i++)
                {
                    threadData = new ThreadData(slice * i, slice * (i + 1));
                    ParameterizedThreadStart ts = useBilinear ? new ParameterizedThreadStart(BilinearScale) : new ParameterizedThreadStart(PointScale);
                    Thread thread = new Thread(ts);
                    thread.Start(threadData);
                }
                threadData = new ThreadData(slice * i, newHeight);
                if (useBilinear)
                {
                    BilinearScale(threadData);
                }
                else
                {
                    PointScale(threadData);
                }
                while (finishCount < cores)
                {
                    Thread.Sleep(1);
                }
            }
            else
            {
                ThreadData threadData = new ThreadData(0, newHeight);
                if (useBilinear)
                {
                    BilinearScale(threadData);
                }
                else
                {
                    PointScale(threadData);
                }
            }

            tex.Resize(newWidth, newHeight);
            tex.SetPixels(newColors);
            tex.Apply();

            texColors = null;
            newColors = null;
        }

        public static void BilinearScale(System.Object obj)
        {
            ThreadData threadData = (ThreadData)obj;
            for (var y = threadData.start; y < threadData.end; y++)
            {
                int yFloor = (int)Mathf.Floor(y * ratioY);
                var y1 = yFloor * w;
                var y2 = (yFloor + 1) * w;
                var yw = y * w2;

                for (var x = 0; x < w2; x++)
                {
                    int xFloor = (int)Mathf.Floor(x * ratioX);
                    var xLerp = x * ratioX - xFloor;
                    newColors[yw + x] = ColorLerpUnclamped(ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp),
                                                           ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp),
                                                           y * ratioY - yFloor);
                }
            }

            mutex.WaitOne();
            finishCount++;
            mutex.ReleaseMutex();
        }

        public static void PointScale(System.Object obj)
        {
            ThreadData threadData = (ThreadData)obj;
            for (var y = threadData.start; y < threadData.end; y++)
            {
                var thisY = (int)(ratioY * y) * w;
                var yw = y * w2;
                for (var x = 0; x < w2; x++)
                {
                    newColors[yw + x] = texColors[(int)(thisY + ratioX * x)];
                }
            }

            mutex.WaitOne();
            finishCount++;
            mutex.ReleaseMutex();
        }

        private static Color ColorLerpUnclamped(Color c1, Color c2, float value)
        {
            return new Color(c1.r + (c2.r - c1.r) * value,
                              c1.g + (c2.g - c1.g) * value,
                              c1.b + (c2.b - c1.b) * value,
                              c1.a + (c2.a - c1.a) * value);
        }
    }
}
