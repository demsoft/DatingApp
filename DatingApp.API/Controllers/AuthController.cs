using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;

namespace DatingApp.API.Controllers
{
    
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _repo;
        private readonly IConfiguration _config;
        private readonly IMapper _mapper;
        public AuthController(IAuthRepository repo, IConfiguration config, IMapper mapper)
        {
            _repo = repo;
            _config = config;
            _mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody]UserForRegisterDto model)
        {
            //validate request
            // if (!ModelState.IsValid)
            // return BadRequest(ModelState);

            model.Username = model.Username.ToLower();

            if (await _repo.UserExists(model.Username))
                return BadRequest("Username already exists");

            var userToCreate = _mapper.Map<User>(model);

            var createdUser = await _repo.Register(userToCreate, model.Password);

            var userToReturn = _mapper.Map<UserForDetailedDto>(createdUser);

            return CreatedAtRoute("Getuser", new {controller = "Users", id= createdUser.Id}, userToReturn);
        }

        [HttpPost("login")]

        public async Task<IActionResult> Login(UserForLoginDtos model){
            //throw new Exception ("Computer Says No");
            var userFromRepo = await _repo.Login(model.Username.ToLower(), model.Password);

            if (userFromRepo == null)
                return Unauthorized();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userFromRepo.Id.ToString()),
                new Claim(ClaimTypes.Name, userFromRepo.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.GetSection("AppSettings:Token").Value));
            
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials =creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = tokenHandler.CreateToken(tokenDescriptor);

            var user = _mapper.Map<UserForListDto>(userFromRepo);


            return Ok(new {
                token = tokenHandler.WriteToken(token),
                user
            });
        }
    }
}