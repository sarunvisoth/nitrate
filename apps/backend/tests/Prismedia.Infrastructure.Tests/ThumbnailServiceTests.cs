using Prismedia.Infrastructure.Media.Processing;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Tests;

public sealed class ThumbnailServiceTests : IDisposable {
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"prismedia-thumbnails-{Guid.NewGuid():N}");

    public ThumbnailServiceTests() {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task TiledJpegComposerWritesAbsoluteConcatFramePaths() {
        var frameDir = Path.Combine(_root, "relative-root", "frames");
        var outputDir = Path.Combine(_root, "relative-root", "tiles");
        Directory.CreateDirectory(frameDir);
        await File.WriteAllTextAsync(Path.Combine(frameDir, "frame-00001.jpg"), "frame1");
        await File.WriteAllTextAsync(Path.Combine(frameDir, "frame-00002.jpg"), "frame2");
        var process = new CapturingProcessExecutor();
        var service = new ThumbnailService(process);

        var tileCount = await service.ComposeTiledJpegSheetsAsync(
            Path.GetRelativePath(Directory.GetCurrentDirectory(), frameDir),
            Path.GetRelativePath(Directory.GetCurrentDirectory(), outputDir),
            columns: 5,
            rows: 5,
            frameWidth: 320,
            frameHeight: 180,
            jpegQuality: 2,
            CancellationToken.None);

        Assert.Equal(1, tileCount);
        Assert.All(process.ConcatLines, line => {
            Assert.StartsWith("file '", line, StringComparison.Ordinal);
            Assert.True(Path.IsPathRooted(line[6..^1]));
        });
    }

    [Fact]
    public async Task ThumbnailAndPreviewUseDolbyVisionToneMappingFilterWhenAvailable() {
        var inputPath = Path.Combine(_root, "dovi.mkv");
        var thumbPath = Path.Combine(_root, "thumb.jpg");
        var previewPath = Path.Combine(_root, "preview.mp4");
        await File.WriteAllTextAsync(inputPath, "source");
        var process = new ProbedVideoProcessExecutor(DolbyVisionProbeJson);
        var service = new ThumbnailService(process, new MediaProbeService(process));

        var result = await service.GenerateThumbnailAndPreviewAsync(
            inputPath,
            thumbPath,
            thumbSeekSeconds: 12,
            thumbWidth: 640,
            thumbHeight: 320,
            thumbQuality: 3,
            previewPath: previewPath,
            previewStartSeconds: 20,
            previewDurationSeconds: 8,
            CancellationToken.None);

        Assert.True(result.Thumbnail);
        Assert.True(result.Preview);
        Assert.Equal(2, process.FfmpegArguments.Count);
        Assert.All(process.FfmpegArguments, arguments => {
            var filterIndex = arguments.ToList().IndexOf("-vf");
            Assert.True(filterIndex >= 0);
            var filter = arguments[filterIndex + 1];
            // Profile 5 tone-maps with tonemapx (applying the Dolby Vision RPU) on the full-res frame
            // before scaling, and without forcing input colour tags.
            Assert.DoesNotContain("setparams", filter);
            Assert.Contains("tonemapx=tonemap=bt2390", filter);
            Assert.Contains("peak=100", filter);
            Assert.Contains("r=tv:format=yuv420p", filter);
            Assert.True(
                filter.IndexOf("tonemapx", StringComparison.Ordinal) <
                filter.IndexOf("scale=", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task ThumbnailAndPreviewUseHdrToneMappingForDolbyVisionProfileEightSources() {
        var inputPath = Path.Combine(_root, "dovi-profile-eight.mkv");
        var thumbPath = Path.Combine(_root, "profile-eight-thumb.jpg");
        var previewPath = Path.Combine(_root, "profile-eight-preview.mp4");
        await File.WriteAllTextAsync(inputPath, "source");
        var process = new ProbedVideoProcessExecutor(DolbyVisionProfileEightProbeJson);
        var service = new ThumbnailService(process, new MediaProbeService(process));

        var result = await service.GenerateThumbnailAndPreviewAsync(
            inputPath,
            thumbPath,
            thumbSeekSeconds: 12,
            thumbWidth: 640,
            thumbHeight: 320,
            thumbQuality: 3,
            previewPath: previewPath,
            previewStartSeconds: 20,
            previewDurationSeconds: 8,
            CancellationToken.None);

        Assert.True(result.Thumbnail);
        Assert.True(result.Preview);
        Assert.Equal(2, process.FfmpegArguments.Count);
        Assert.All(process.FfmpegArguments, arguments => {
            var filterIndex = arguments.ToList().IndexOf("-vf");
            Assert.True(filterIndex >= 0);
            var filter = arguments[filterIndex + 1];
            Assert.DoesNotContain("tonemapx", filter);
            Assert.Contains("zscale=t=linear", filter);
            Assert.Contains("tonemap=tonemap=hable", filter);
            Assert.Contains("zscale=t=bt709:m=bt709:p=bt709", filter);
        });
    }

    [Fact]
    public async Task TrickplayExtractionUsesHdrToneMappingFilter() {
        var inputPath = Path.Combine(_root, "hdr10.mkv");
        var frameDir = Path.Combine(_root, "frames");
        await File.WriteAllTextAsync(inputPath, "source");
        var process = new ProbedVideoProcessExecutor(Hdr10ProbeJson);
        var service = new ThumbnailService(process, new MediaProbeService(process));

        var count = await service.ExtractTrickplayFramesBatchAsync(
            inputPath,
            frameDir,
            duration: 12,
            intervalSeconds: 6,
            width: 320,
            height: 180,
            jpegQuality: 3,
            CancellationToken.None);

        Assert.True(count >= 1);

        // Trickplay extraction is a single decode pass: one ffmpeg invocation samples
        // every keyframe via the fps filter rather than spawning one process per frame.
        Assert.Single(process.FfmpegArguments);
        var arguments = process.FfmpegArguments[0];
        Assert.Contains("-skip_frame", arguments);
        Assert.Contains("image2", arguments);

        var filterIndex = arguments.ToList().IndexOf("-vf");
        Assert.True(filterIndex >= 0);
        var filter = arguments[filterIndex + 1];
        Assert.StartsWith("fps=1/6,", filter);
        Assert.Contains("zscale=t=linear", filter);
        Assert.Contains("tonemap=tonemap=hable", filter);
        Assert.Contains("zscale=t=bt709:m=bt709:p=bt709", filter);
        Assert.Contains("format=yuvj420p", filter);
    }

    [Fact]
    public async Task AudioWaveformReadsBinaryPcmFromProcessOutputFile() {
        var inputPath = Path.Combine(_root, "audio.m4a");
        await File.WriteAllTextAsync(inputPath, "source");
        var process = new BinaryWaveformProcessExecutor([-1000, 1000, -250, 250]);
        var service = new ThumbnailService(process);

        var data = await service.GenerateWaveformDataAsync(
            inputPath,
            durationSeconds: 1,
            pixelsPerSecond: 2,
            CancellationToken.None);

        Assert.NotNull(data);
        Assert.Equal([-1000, 1000, -250, 250], data);
        Assert.Single(process.OutputPaths);
        Assert.False(File.Exists(process.OutputPaths[0]));
    }

    public void Dispose() {
        if (Directory.Exists(_root)) {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class CapturingProcessExecutor : ProcessExecutor {
        public IReadOnlyList<string> ConcatLines { get; private set; } = [];

        public override async Task<ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken, bool lowPriority = false) {
            var inputIndex = arguments.ToList().IndexOf("-i");
            Assert.True(inputIndex >= 0);
            ConcatLines = await File.ReadAllLinesAsync(arguments[inputIndex + 1], cancellationToken);
            var outputPath = arguments[^1];
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, "tile", cancellationToken);
            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        }
    }

    private sealed class BinaryWaveformProcessExecutor(short[] samples) : ProcessExecutor {
        public List<string> OutputPaths { get; } = [];

        public override Task<ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken, bool lowPriority = false) =>
            Task.FromResult(new ProcessExecutionResult(1, string.Empty, "stdout text path used"));

        public override async Task<ProcessExecutionResult> RunToFileAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            string outputPath,
            CancellationToken cancellationToken, bool lowPriority = false) {
            OutputPaths.Add(outputPath);
            var bytes = new byte[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++) {
                var valueBytes = BitConverter.GetBytes(samples[i]);
                bytes[i * 2] = valueBytes[0];
                bytes[i * 2 + 1] = valueBytes[1];
            }
            await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        }
    }

    private sealed class ProbedVideoProcessExecutor(string probeJson) : ProcessExecutor {
        public List<IReadOnlyList<string>> FfmpegArguments { get; } = [];
        public List<string> FfmpegFileNames { get; } = [];
        public List<string> FfprobeFileNames { get; } = [];

        public override async Task<ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken, bool lowPriority = false) {
            if (Path.GetFileName(fileName).Equals("ffprobe", StringComparison.OrdinalIgnoreCase)) {
                FfprobeFileNames.Add(fileName);
                return new ProcessExecutionResult(0, probeJson, string.Empty);
            }

            FfmpegFileNames.Add(fileName);
            FfmpegArguments.Add(arguments);
            var outputPath = arguments[^1];
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, "asset", cancellationToken);
            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        }
    }

    private const string DolbyVisionProbeJson = """
        {
          "format": { "duration": "42", "format_name": "matroska" },
          "streams": [
            {
              "index": 0,
              "codec_type": "video",
              "codec_name": "hevc",
              "pix_fmt": "yuv420p10le",
              "width": 3840,
              "height": 1920,
              "avg_frame_rate": "24000/1001",
              "color_range": "pc",
              "side_data_list": [
                {
                  "side_data_type": "DOVI configuration record",
                  "dv_profile": 5,
                  "dv_level": 6,
                  "rpu_present_flag": 1,
                  "el_present_flag": 0,
                  "bl_present_flag": 1,
                  "dv_bl_signal_compatibility_id": 0
                }
              ],
              "disposition": { "default": 1, "forced": 0 }
            }
          ]
        }
        """;

    private const string DolbyVisionProfileEightProbeJson = """
        {
          "format": { "duration": "42", "format_name": "matroska" },
          "streams": [
            {
              "index": 0,
              "codec_type": "video",
              "codec_name": "hevc",
              "pix_fmt": "yuv420p10le",
              "width": 3840,
              "height": 1920,
              "avg_frame_rate": "24000/1001",
              "color_range": "tv",
              "color_transfer": "smpte2084",
              "color_primaries": "bt2020",
              "color_space": "bt2020nc",
              "side_data_list": [
                {
                  "side_data_type": "DOVI configuration record",
                  "dv_profile": 8,
                  "dv_level": 6,
                  "rpu_present_flag": 1,
                  "el_present_flag": 0,
                  "bl_present_flag": 1,
                  "dv_bl_signal_compatibility_id": 1
                }
              ],
              "disposition": { "default": 1, "forced": 0 }
            }
          ]
        }
        """;

    private const string Hdr10ProbeJson = """
        {
          "format": { "duration": "42", "format_name": "matroska" },
          "streams": [
            {
              "index": 0,
              "codec_type": "video",
              "codec_name": "hevc",
              "pix_fmt": "yuv420p10le",
              "width": 3840,
              "height": 2160,
              "avg_frame_rate": "24/1",
              "color_range": "tv",
              "color_space": "bt2020nc",
              "color_transfer": "smpte2084",
              "color_primaries": "bt2020",
              "disposition": { "default": 1, "forced": 0 }
            }
          ]
        }
        """;
}
