using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SQCScanner.Services
{
    public class DigitTemplateReader
    {
        private static readonly string TemplateFolder = Path.Combine(AppContext.BaseDirectory, "DigitTemplates");
        private static readonly string UnlabeledFolder = Path.Combine(
            @"D:\Prashant_Devloper\ImageBaseOMR\FrontEnd\OMR_ImageBase_BackEnd_V1\Version1\wFileManager\bulk_scan\Text ERROR",
            "Unlabeled");

        private const int NormW = 32, NormH = 48, MinComponentPx = 8, MinGapPx = 4;
        private static readonly double MinConfidence = 0.60;

        private static readonly object Lock = new();
        private static Dictionary<char, List<bool[,]>>? _templates;

        public static string ReadNumber(Image<Rgba32> charBox)
        {
            EnsureTemplatesLoaded();
            try
            {
                using var gray = charBox.CloneAs<L8>();
                var mask = RemoveSmallComponents(MorphClose(Binarize(gray)), MinComponentPx);

                SaveMaskToDisk(mask, $"transformed_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

                var segments = SegmentColumns(mask, MinGapPx);
                if (segments.Count == 0) { SaveForReview(charBox, "no_segments"); return string.Empty; }

                var chars = new List<char>();
                bool uncertain = false;

                foreach (var (x0, x1) in segments)
                {
                    var glyph = CropToContent(mask, x0, x1);
                    if (glyph.GetLength(0) == 0 || glyph.GetLength(1) == 0) continue;

                    var (digit, score) = MatchTemplate(Normalize(glyph, NormW, NormH));
                    if (score < MinConfidence) { uncertain = true; chars.Add('?'); }
                    else chars.Add(digit);
                }

                string result = new string(chars.ToArray());
                if (uncertain) SaveForReview(charBox, "uncertain_" + result.Replace('?', 'x'));
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DigitTemplateReader Error: {ex.Message}");
                SaveForReview(charBox, "exception");
                return string.Empty;
            }
        }

        private static bool[,] Binarize(Image<L8> gray)
        {
            int w = gray.Width, h = gray.Height;
            var hist = new int[256];

            gray.ProcessPixelRows(a => {
                for (int y = 0; y < h; y++)
                {
                    var row = a.GetRowSpan(y);
                    for (int x = 0; x < w; x++) hist[row[x].PackedValue]++;
                }
            });

            int total = w * h;
            long sumAll = 0;
            for (int t = 0; t < 256; t++) sumAll += (long)t * hist[t];

            long sumB = 0;
            int wB = 0;
            double maxVar = -1;
            int thr = 128;

            for (int t = 0; t < 256; t++)
            {
                wB += hist[t];
                if (wB == 0) continue;
                int wF = total - wB;
                if (wF == 0) break;
                sumB += (long)t * hist[t];
                double mB = (double)sumB / wB, mF = (double)(sumAll - sumB) / wF;
                double v = (double)wB * wF * (mB - mF) * (mB - mF);
                if (v > maxVar) { maxVar = v; thr = t; }
            }

            bool bgLight = hist[..thr].Sum() < hist[thr..].Sum();
            var mask = new bool[h, w];

            gray.ProcessPixelRows(a => {
                for (int y = 0; y < h; y++)
                {
                    var row = a.GetRowSpan(y);
                    for (int x = 0; x < w; x++)
                    {
                        byte v = row[x].PackedValue;
                        mask[y, x] = bgLight ? v < thr : v > thr;
                    }
                }
            });

            return mask;
        }

        private static void SaveMaskToDisk(bool[,] mask, string filename)
        {
            int h = mask.GetLength(0);
            int w = mask.GetLength(1);
            if (w == 0 || h == 0) return;

            using var img = new Image<L8>(w, h);
            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < w; x++)
                    {
                        row[x] = new L8((byte)(mask[y, x] ? 0 : 255));
                    }
                }
            });

            Directory.CreateDirectory(UnlabeledFolder);
            img.SaveAsPng(Path.Combine(UnlabeledFolder, filename));
        }

        public static void AddTemplate(char digit, Image<Rgba32> sampleGlyphCrop)
        {
            if (!char.IsDigit(digit)) throw new ArgumentException("Only digits 0-9 are supported.", nameof(digit));
            Directory.CreateDirectory(TemplateFolder);
            sampleGlyphCrop.SaveAsPng(Path.Combine(TemplateFolder, $"{digit}_{Guid.NewGuid():N}.png"));
            lock (Lock) { _templates = null; }
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_templates != null) return;
            lock (Lock)
            {
                if (_templates != null) return;
                var dict = new Dictionary<char, List<bool[,]>>();
                Directory.CreateDirectory(TemplateFolder);

                foreach (var file in Directory.GetFiles(TemplateFolder, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.Length == 0 || !char.IsDigit(name[0])) continue;

                    using var img = Image.Load<L8>(file);
                    var normalized = Normalize(TrimToContent(Binarize(img)), NormW, NormH);
                    if (!dict.TryGetValue(name[0], out var list)) dict[name[0]] = list = new List<bool[,]>();
                    list.Add(normalized);
                }

                if (dict.Count < 10)
                    Console.WriteLine($"WARNING: digit templates incomplete. Have: [{string.Join(",", dict.Keys.OrderBy(c => c))}]. " +
                        "Add PNGs named '0_x.png'..'9_x.png' to " + TemplateFolder);

                _templates = dict;
            }
        }

        private static bool[,] MorphClose(bool[,] m) => Morph(Morph(m, true), false);

        private static bool[,] Morph(bool[,] m, bool dilate)
        {
            int h = m.GetLength(0), w = m.GetLength(1);
            var o = new bool[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool r = !dilate;
                    for (int dy = -1; dy <= 1 && r == !dilate; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int ny = y + dy, nx = x + dx;
                            bool v = ny >= 0 && ny < h && nx >= 0 && nx < w && m[ny, nx];
                            r = dilate ? r | v : r & v;
                        }
                    o[y, x] = r;
                }
            return o;
        }

        private static bool[,] RemoveSmallComponents(bool[,] mask, int minPixels)
        {
            int h = mask.GetLength(0), w = mask.GetLength(1);
            var visited = new bool[h, w]; var result = new bool[h, w];
            var stack = new Stack<(int y, int x)>();

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!mask[y, x] || visited[y, x]) continue;
                    var comp = new List<(int, int)>();
                    stack.Push((y, x)); visited[y, x] = true;
                    while (stack.Count > 0)
                    {
                        var (cy, cx) = stack.Pop(); comp.Add((cy, cx));
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int ny = cy + dy, nx = cx + dx;
                                if (ny < 0 || ny >= h || nx < 0 || nx >= w || visited[ny, nx] || !mask[ny, nx]) continue;
                                visited[ny, nx] = true; stack.Push((ny, nx));
                            }
                    }
                    if (comp.Count >= minPixels) foreach (var (py, px) in comp) result[py, px] = true;
                }
            return result;
        }

        private static List<(int x0, int x1)> SegmentColumns(bool[,] mask, int minGap)
        {
            int h = mask.GetLength(0), w = mask.GetLength(1);
            var col = new bool[w];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (mask[y, x]) { col[x] = true; break; }

            var raw = new List<(int, int)>(); int? start = null;
            for (int x = 0; x < w; x++)
            {
                if (col[x] && start == null) start = x;
                else if (!col[x] && start != null) { raw.Add((start.Value, x)); start = null; }
            }
            if (start != null) raw.Add((start.Value, w));
            raw = raw.Where(s => s.Item2 - s.Item1 >= 2).ToList();

            var merged = new List<(int x0, int x1)>();
            foreach (var seg in raw)
            {
                if (merged.Count > 0 && seg.Item1 - merged[^1].x1 < minGap) merged[^1] = (merged[^1].x0, seg.Item2);
                else merged.Add((seg.Item1, seg.Item2));
            }
            return merged;
        }

        private static bool[,] CropToContent(bool[,] mask, int x0, int x1)
        {
            int h = mask.GetLength(0); int minY = int.MaxValue, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = x0; x < x1; x++)
                    if (mask[y, x]) { if (y < minY) minY = y; if (y > maxY) maxY = y; break; }
            if (maxY < minY) return new bool[0, 0];

            int oh = maxY - minY + 1, ow = x1 - x0;
            var r = new bool[oh, ow];
            for (int y = 0; y < oh; y++) for (int x = 0; x < ow; x++) r[y, x] = mask[minY + y, x0 + x];
            return r;
        }

        private static bool[,] TrimToContent(bool[,] mask)
        {
            int h = mask.GetLength(0), w = mask.GetLength(1);
            int minX = w, maxX = -1, minY = h, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (mask[y, x]) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < minX || maxY < minY) return mask;

            int oh = maxY - minY + 1, ow = maxX - minX + 1;
            var r = new bool[oh, ow];
            for (int y = 0; y < oh; y++) for (int x = 0; x < ow; x++) r[y, x] = mask[minY + y, minX + x];
            return r;
        }

        private static bool[,] Normalize(bool[,] glyph, int targetW, int targetH)
        {
            int gh = glyph.GetLength(0), gw = glyph.GetLength(1);
            if (gh == 0 || gw == 0) return new bool[targetH, targetW];

            double scale = Math.Min((double)targetW / gw, (double)targetH / gh);
            int sw = Math.Max(1, (int)Math.Round(gw * scale)), sh = Math.Max(1, (int)Math.Round(gh * scale));

            var scaled = new bool[sh, sw];
            for (int y = 0; y < sh; y++)
            {
                int srcY = Math.Min(gh - 1, (int)(y / scale));
                for (int x = 0; x < sw; x++)
                    scaled[y, x] = glyph[srcY, Math.Min(gw - 1, (int)(x / scale))];
            }

            var canvas = new bool[targetH, targetW];
            int offY = (targetH - sh) / 2, offX = (targetW - sw) / 2;
            for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++) canvas[offY + y, offX + x] = scaled[y, x];
            return canvas;
        }

        private static (char digit, double score) MatchTemplate(bool[,] normalized)
        {
            char best = '?'; double bestScore = -1;
            if (_templates == null) return (best, 0);

            foreach (var (digit, samples) in _templates)
                foreach (var sample in samples)
                {
                    double score = PixelOverlapScore(normalized, sample);
                    if (score > bestScore) { bestScore = score; best = digit; }
                }
            return (best, bestScore);
        }

        private static double PixelOverlapScore(bool[,] a, bool[,] b)
        {
            int h = a.GetLength(0), w = a.GetLength(1), match = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (a[y, x] == b[y, x]) match++;
            return (double)match / (h * w);
        }

        private static void SaveForReview(Image<Rgba32> charBox, string tag)
        {
            try
            {
                Directory.CreateDirectory(UnlabeledFolder);
                charBox.SaveAsPng(Path.Combine(UnlabeledFolder, $"{tag}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png"));
            }
            catch (Exception ex) { Console.WriteLine($"DigitTemplateReader: review save failed: {ex.Message}"); }
        }
    }
}