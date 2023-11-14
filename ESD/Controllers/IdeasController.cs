﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ESD.Data;
using ESD.Models;
using Microsoft.AspNetCore.Identity;
using ESD.Services.EmailService;

using PagedList;

namespace ESD.Controllers
{
    public class IdeasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;

        public IdeasController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // GET: Ideas
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString, int? page)
        {
            ViewBag.CurrentSort = sortOrder;
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            var users = _userManager.Users.ToList();

            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }
            ViewBag.CurrentFilter = searchString;


            ViewData["users"] = users;
            ViewData["currentUser"] = currentUser;

            int pageSize = 5;
            int pageNumber = (page ?? 1);

            var applicationDbContext = _context.Ideas.Include(i => i.Category).Include(i => i.Topic);

            return View(applicationDbContext.ToPagedList(pageNumber, pageSize));
        }

        // GET: Ideas/Search
        public async Task<IActionResult> Search()
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            var users = _userManager.Users.ToList();

            ViewData["users"] = users;
            ViewData["currentUser"] = currentUser;

            var applicationDbContext = _context.Ideas.Include(i => i.Category).Include(i => i.Topic);
            return View(await applicationDbContext.ToListAsync());
        }

        // POST: Ideas/SearchResults
        public async Task<IActionResult> SearchResults(string SearchPhrase)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            var users = _userManager.Users.ToList();

            ViewData["users"] = users;
            ViewData["currentUser"] = currentUser;

            var applicationDbContext = _context.Ideas.Include(i => i.Category).Include(i => i.Topic);
            return View("Index", await applicationDbContext.Where(j => j.Text.Contains(SearchPhrase)).ToListAsync());
        }

        // GET: Ideas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Ideas == null)
            {
                return NotFound();
            }

            int ideaID = (int)id;
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var idea = await _context.Ideas
                .Include(i => i.Category)
                .Include(i => i.Topic)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (!ViewExist(ideaID, user.Id))
            {
                AddView(ideaID, user.Id, idea);
                await _context.SaveChangesAsync();
            }

            if (idea == null)
            {
                return NotFound();
            }

            ViewData["Comments"] = _context.Comments.Where(c => c.IdeaId == id).ToList();

            TempData["CurrentIdea"] = id;

            return View(idea);
        }

        public void AddView(int ideaID, string UserID, Idea idea)
        {
            View view = new View();
            view.IdeaId = ideaID;
            view.UserId = UserID;

            idea.ViewS += 1;

            _context.Add(view);
        }
                
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            ViewData["TopicId"] = new SelectList(_context.Topics, "Id", "Name");
            return View();
        }
                
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Text,FilePath,Datetime,IsAnomynous,LikeS,DislikeS,ViewS,UserId,CategoryId,TopicId")] Idea idea)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);

            //if (ModelState.IsValid)
            {
                idea.FilePath = String.Empty;
                idea.UserId = user.Id;
                idea.DateTime = DateTime.Now;

                //SendEmail(user.Id);

                _context.Add(idea);
                await _context.SaveChangesAsync();                         

                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Id", idea.CategoryId);
            ViewData["TopicId"] = new SelectList(_context.Topics, "Id", "Id", idea.TopicId);
            return View(idea);
        }

        //React
        public async Task<IActionResult> React(int id, bool reactT)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (!ReactExist(id, user.Id))
            {
                React react = new React();
                react.IdeaId = id;
                react.UserId = user.Id;
                react.IsLiked = reactT;

                _context.Add(react);

                InputIdeaReact(reactT, id);
            }
            else
            {
                ChangeReact(reactT, id, user.Id);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public void ChangeReact(bool react, int ideaID, string UserID)
        {
            React updateR = _context.Reacts.Where(r => r.IdeaId == ideaID).Where(r => r.UserId == UserID).FirstOrDefault();

            if (ReactDifferent(react, updateR))
            {
                UpdateIdeaReact(react, ideaID);
                updateR.IsLiked = react;
            }
            else
            {
                DeleteIdeaReact(react, ideaID);
                _context.Reacts.Remove(updateR);
            }
        }

        public void InputIdeaReact(bool react, int ideaID)
        {
            Idea update = _context.Ideas.ToList().Find(i => i.Id == ideaID);

            if (react)
            {
                update.LikeS += 1;
            }
            else
            {
                update.DislikeS += 1;
            }
        }

        public void UpdateIdeaReact(bool react, int ideaID)
        {
            Idea update = _context.Ideas.ToList().Find(i => i.Id == ideaID);

            if (react)
            {
                update.DislikeS -= 1;
            }
            else
            {
                update.LikeS -= 1;
            }

            InputIdeaReact(react, ideaID);
        }

        public void DeleteIdeaReact(bool react, int ideaID)
        {
            Idea update = _context.Ideas.ToList().Find(i => i.Id == ideaID);

            if (react)
            {
                update.LikeS -= 1;
            }
            else
            {
                update.DislikeS -= 1;
            }
        }

        // GET: Ideas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Ideas == null)
            {
                return NotFound();
            }

            var idea = await _context.Ideas.FindAsync(id);

            if (idea == null)
            {
                return NotFound();
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Id", idea.CategoryId);
            ViewData["TopicId"] = new SelectList(_context.Topics, "Id", "Id", idea.TopicId);
            return View(idea);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Text,FilePath,Datetime,IsAnomynous,LikeS,DislikeS,ViewS,UserId,CategoryId,TopicId")] Idea idea)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);

            if (id != idea.Id)
            {
                return NotFound();
            }

            //if (ModelState.IsValid)
            {
                try
                {
                    //SendEmail(user.Email, "Idea Edited", "Change your mind?");
                    _context.Update(idea);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IdeaExists(idea.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Id", idea.CategoryId);
            ViewData["TopicId"] = new SelectList(_context.Topics, "Id", "Id", idea.TopicId);
            return View(idea);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Ideas == null)
            {
                return NotFound();
            }

            var idea = await _context.Ideas
                .Include(i => i.Category)
                .Include(i => i.Topic)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (idea == null)
            {
                return NotFound();
            }

            return View(idea);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Ideas == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Ideas'  is null.");
            }
            var idea = await _context.Ideas.FindAsync(id);
            if (idea != null)
            {
                _context.Ideas.Remove(idea);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        //Confirmation
        public IActionResult TermConfirm()
        {
            return View();
        }

        private bool IdeaExists(int id)
        {
          return (_context.Ideas?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        private bool ReactExist(int ideaID, string UserID)
        {
            React reactE = _context.Reacts.Where(r => r.IdeaId == ideaID).Where(r => r.UserId == UserID).FirstOrDefault();

            if (reactE != null)
            {
                return true;
            }

            return false;

        }

        private bool ReactDifferent(bool react, React updateR)
        {
            if (updateR.IsLiked != react)
            {
                return true;
            }
            return false;
        }

        private bool ViewExist(int ideaID, string UserID)
        {
            View viewE = _context.Views.Where(v => v.IdeaId == ideaID).Where(v => v.UserId == UserID).FirstOrDefault();

            if (viewE != null)
            {
                return true;
            }

            return false;
        }

        public void SendEmail(string UserID)
        {
            //var ideaMaker = _context.Users.FirstOrDefault(i => i.Id == UserID);
            var departmentUser = _context.DepartmentUsers.FirstOrDefault(d => d.UserId == UserID);
            var makerDepartment = _context.Departments.FirstOrDefault(m => m.Id == departmentUser.DepartmentId);

            makerDepartment.IdeasS += 1;

            string departmentHeadRole = makerDepartment.Name + " Head";

            var role = _context.Roles.FirstOrDefault(r => r.Name == departmentHeadRole);
            var userRole = _context.UserRoles.FirstOrDefault(u => u.RoleId == role.Id);
            var headUser = _context.Users.FirstOrDefault(h => h.Id == userRole.UserId);

            EmailDio emailDio = new EmailDio();

            emailDio.To = headUser.Email;
            emailDio.Subject = "Idea created notification.";
            emailDio.Body = "There is someone from your department created a idea.";

            _emailService.SendEmail(emailDio);
        }
    }
}
