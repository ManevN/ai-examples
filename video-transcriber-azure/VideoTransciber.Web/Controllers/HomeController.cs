using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VideoTransciber.Web.Models;

namespace VideoTransciber.Web.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VideoTransciber.Web.Services;

[Route("/")]
public class HomeController : Controller
{
    private readonly VideoService _videoService;
    private readonly BlobService _blobService;
    private readonly SpeechService _speechService;

    public HomeController(VideoService videoService, BlobService blobService, SpeechService speechService)
    {
        _videoService = videoService;
        _blobService = blobService;
        _speechService = speechService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Transcribe(string videoUrl)
    {
        var audioPath = await _videoService.ExtractAudioAsync(videoUrl);
        var blobUrl = await _blobService.UploadAudioAsync(audioPath);
        var transcript = await _speechService.TranscribeAsync(blobUrl);
        ViewBag.Transcript = transcript;
        return View("Index");
    }
}
