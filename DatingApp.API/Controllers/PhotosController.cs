using AutoMapper;
using DatingApp.API.Data;
using DatingApp.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using CloudinaryDotNet;
using System.Threading.Tasks;
using DatingApp.API.Dtos;
using System.Security.Claims;
using DatingApp.API.Models;
using CloudinaryDotNet.Actions;
using System.Linq;

namespace DatingApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private readonly IDatingRepository _repo;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repo, IMapper mapper, IOptions<CloudinarySettings> cloudinaryConfig)
        {
            this._mapper = mapper;
            this._cloudinaryConfig = cloudinaryConfig;
            this._repo = repo;

            Account acc = new Account(
                _cloudinaryConfig.Value.CloudName,
                _cloudinaryConfig.Value.ApiKey,
                _cloudinaryConfig.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(acc);
        }

        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int Id)
        {
            var photoFromRepo = await _repo.GetPhoto(Id);

            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);

            return Ok(photo);
        }

        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId, [FromForm]PhotoForCreationDto model)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();
            
            var userFromRepo = await _repo.GetUser(userId);

            var file = model.File;

            var uploadResult = new ImageUploadResult();

            if (file.Length > 0){
                using (var stream = file.OpenReadStream()){
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                    };

                    uploadResult = _cloudinary.Upload(uploadParams);
                }
            }
            model.Url = uploadResult.Uri.ToString();
            model.PublicId = uploadResult.PublicId;

            var photo = _mapper.Map<Photo>(model);

            if (!userFromRepo.Photos.Any(u => u.IsMain))
                photo.IsMain = true;
            
            userFromRepo.Photos.Add(photo);          

            if(await _repo.SaveAll())
            {
                var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                return CreatedAtRoute("GetPhoto", new {controller = "Photos", id = photo.Id}, photoToReturn);
            }

            return BadRequest("Could not add the photo");
        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id)
        {
             if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var user = await _repo.GetUser(userId);

            if (!user.Photos.Any(p => p.Id == id))
                return Unauthorized();

            var photo = await _repo.GetPhoto(id);
            if(photo.IsMain)
                return BadRequest("This is already the main photo");

            var currentMainPhoto = await _repo.GetMainPhotoForUser(userId);
            currentMainPhoto.IsMain = false;

            photo.IsMain = true;

            if (await _repo.SaveAll())
                return NoContent();

            return BadRequest("Could not set photo to main");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var user = await _repo.GetUser(userId);

            if (!user.Photos.Any(p => p.Id == id))
                return Unauthorized();

            var photo = await _repo.GetPhoto(id);
            if (photo.IsMain)
                return BadRequest("You cannot delete your main photo");

            if (photo.PublicId != null)
            {
                 var deleteParams = new DeletionParams(photo.PublicId);
                 var result = _cloudinary.Destroy(deleteParams);

                if (result.Result == "ok"){
                    _repo.Delete(photo);
                }
            }

           if (photo.PublicId == null)
           {
               _repo.Delete(photo);
           }
            if (await _repo.SaveAll())
                return Ok();
            
            return BadRequest("Failed to delete the photo");
        }
        
    }
}