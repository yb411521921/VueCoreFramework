﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using VueCoreFramework.Data;
using VueCoreFramework.Models;
using VueCoreFramework.Services;

namespace VueCoreFramework.Controllers
{
    /// <summary>
    /// An MVC controller for handling messaging tasks.
    /// </summary>
    [Authorize]
    [Route("api/[controller]/[action]")]
    public class MessageController : Controller
    {
        private readonly AdminOptions _adminOptions;
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        /// <summary>
        /// Initializes a new instance of <see cref="MessageController"/>.
        /// </summary>
        public MessageController(
            IOptions<AdminOptions> adminOptions,
            ApplicationDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _adminOptions = adminOptions.Value;
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        /// <summary>
        /// Called to get a list of users involved in individual conversations in which the current
        /// user is a sender or recipient, with an unread message count.
        /// </summary>
        /// <returns>
        /// An error if there is a problem; or a list of <see cref="ConversationViewModel"/>s.
        /// </returns>
        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var email = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { error = ErrorMessages.InvalidUserError });
            }
            if (user.AdminLocked)
            {
                return Json(new { error = ErrorMessages.LockedAccount(_adminOptions.AdminEmailAddress) });
            }

            List<ConversationViewModel> vms = new List<ConversationViewModel>();
            foreach(var message in _context.Messages
                .Where(m => m.Sender == user || m.SingleRecipient == user))
            {
                var interlocutor = message.Sender == user ? message.SingleRecipientName : message.SenderUsername;
                var conversation = vms.FirstOrDefault(v => v.Interlocutor == interlocutor);
                if (conversation == null)
                {
                    conversation = new ConversationViewModel { Interlocutor = interlocutor };
                    vms.Add(conversation);
                }
                if (message.SingleRecipient == user && !message.Received)
                {
                    conversation.UnreadCount++;
                }
            }
            return Json(vms);
        }

        /// <summary>
        /// Called to get the messages exchanged within the given group.
        /// </summary>
        /// <param name="group">The name of the group whose conversation will be retrieved.</param>
        /// <returns>
        /// An error if there is a problem; or the ordered list of <see cref="MessageViewModel"/>s.
        /// </returns>
        [HttpGet("{group}")]
        public async Task<IActionResult> GetGroupMessages(string group)
        {
            var email = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { error = ErrorMessages.InvalidUserError });
            }
            if (user.AdminLocked)
            {
                return Json(new { error = ErrorMessages.LockedAccount(_adminOptions.AdminEmailAddress) });
            }

            var groupRole = await _roleManager.FindByNameAsync(group);
            if (groupRole == null)
            {
                return Json(new { error = ErrorMessages.InvalidTargetGroupError });
            }

            return Json(_context.Messages.Where(m => m.GroupRecipient == groupRole)
                .OrderBy(m => m.Timestamp)
                .Select(m => new MessageViewModel
                {
                    Content = m.Content,
                    IsSystemMessage = m.IsSystemMessage,
                    Username = m.SenderUsername,
                    Timestamp = m.Timestamp
                }));
        }

        /// <summary>
        /// Called to get the messages between the current user and the given user which have not
        /// been marked deleted by the current user.
        /// </summary>
        /// <param name="username">
        /// The name of the user whose conversation with the current user will be retrieved.
        /// </param>
        /// <returns>
        /// An error if there is a problem; or the ordered list of <see cref="MessageViewModel"/>s.
        /// </returns>
        [HttpGet("{username}")]
        public async Task<IActionResult> GetUserMessages(string username)
        {
            var email = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { error = ErrorMessages.InvalidUserError });
            }
            if (user.AdminLocked)
            {
                return Json(new { error = ErrorMessages.LockedAccount(_adminOptions.AdminEmailAddress) });
            }
            
            return Json(_context.Messages.Where(m =>
               (m.SingleRecipient == user && m.SenderUsername == username && !m.RecipientDeleted)
               || (m.SingleRecipientName == username && m.Sender == user && !m.SenderDeleted))
                .OrderBy(m => m.Timestamp)
                .Select(m => new MessageViewModel
                {
                    Content = m.Content,
                    Username = m.Sender == user ? m.SingleRecipientName : m.SenderUsername,
                    Timestamp = m.Timestamp
                }));
        }

        /// <summary>
        /// Called to mark a conversation with a given user deleted.
        /// </summary>
        /// <param name="username">
        /// The name of the user whose conversation with the current user will be marked deleted.
        /// </param>
        /// <returns>An error if there is a problem; or a response indicating success.</returns>
        [HttpPost("{username}")]
        public async Task<IActionResult> MarkConversationDeleted(string username)
        {
            var email = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { error = ErrorMessages.InvalidUserError });
            }
            if (user.AdminLocked)
            {
                return Json(new { error = ErrorMessages.LockedAccount(_adminOptions.AdminEmailAddress) });
            }

            foreach (var message in _context.Messages.Where(m =>
                (m.SingleRecipient == user && m.SenderUsername == username)
                || (m.SingleRecipientName == username && m.Sender == user)))
            {
                if (message.Sender == user)
                {
                    message.SenderDeleted = true;
                }
                else
                {
                    message.RecipientDeleted = true;
                }
                // Messages are actually deleted once both participants have marked them as such.
                if (message.SenderDeleted && message.RecipientDeleted)
                {
                    _context.Messages.Remove(message);
                }
            }
            await _context.SaveChangesAsync();

            return Json(new { response = ResponseMessages.Success });
        }

        /// <summary>
        /// Called to mark a conversation with a given user read, from the perspective of the current user.
        /// </summary>
        /// <param name="username">
        /// The name of the user whose conversation with the current user will be marked read.
        /// </param>
        /// <returns>An error if there is a problem; or a response indicating success.</returns>
        [HttpPost("{username}")]
        public async Task<IActionResult> MarkConversationRead(string username)
        {
            var email = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { error = ErrorMessages.InvalidUserError });
            }
            if (user.AdminLocked)
            {
                return Json(new { error = ErrorMessages.LockedAccount(_adminOptions.AdminEmailAddress) });
            }

            foreach (var message in _context.Messages.Where(m => m.SingleRecipient == user
                && m.SenderUsername == username))
            {
                message.Received = true;
            }
            await _context.SaveChangesAsync();

            return Json(new { response = ResponseMessages.Success });
        }

        /// <summary>
        /// Called to send a message to the given group.
        /// </summary>
        /// <param name="group">The name of the group to which the message will be sent.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>An error if there is a problem; or a response indicating success.</returns>
        [HttpPost("{group}")]
        public async Task<IActionResult> SendMessageToGroup(string group, [FromBody]string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length > 125)
            {
                return Json(new { error = ErrorMessages.MessageInvalidLengthError });
            }
            var email = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { error = ErrorMessages.InvalidUserError });
            }
            if (user.AdminLocked)
            {
                return Json(new { error = ErrorMessages.LockedAccount(_adminOptions.AdminEmailAddress) });
            }

            var groupRole = await _roleManager.FindByNameAsync(group);
            if (groupRole == null)
            {
                return Json(new { error = ErrorMessages.InvalidTargetGroupError });
            }

            var messages = _context.Messages.Where(m => m.GroupRecipient == groupRole);
            if (messages.Count() >= 250)
            {
                _context.Messages.Remove(messages.OrderBy(m => m.Timestamp).FirstOrDefault());
            }
            _context.Messages.Add(new Message
            {
                Content = message,
                Sender = user,
                SenderUsername = user.UserName,
                GroupRecipient = groupRole,
                GroupRecipientName = groupRole.Name
            });
            await _context.SaveChangesAsync();
            return Json(new { response = ResponseMessages.Success });
        }

        /// <summary>
        /// Called to send a message to the given user.
        /// </summary>
        /// <param name="username">The name of the user to whom the message will be sent.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>An error if there is a problem; or a response indicating success.</returns>
        [HttpPost("{username}")]
        public async Task<IActionResult> SendMessageToUser(string username, [FromBody]string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length > 125)
            {
                return Json(new { error = ErrorMessages.MessageInvalidLengthError });
            }
            var email = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(new { error = ErrorMessages.InvalidUserError });
            }
            if (user.AdminLocked)
            {
                return Json(new { error = ErrorMessages.LockedAccount(_adminOptions.AdminEmailAddress) });
            }

            var targetUser = await _userManager.FindByNameAsync(username);
            if (targetUser == null)
            {
                return Json(new { error = ErrorMessages.InvalidTargetUserError });
            }

            var messages = _context.Messages.Where(m =>
                (m.Sender == user && m.SingleRecipient == targetUser)
                || (m.Sender == targetUser && m.SingleRecipient == user));
            if (messages.Count() >= 100)
            {
                _context.Messages.Remove(messages.OrderBy(m => m.Timestamp).FirstOrDefault());
            }
            _context.Messages.Add(new Message
            {
                Content = message,
                Sender = user,
                SenderUsername = user.UserName,
                SingleRecipient = targetUser,
                SingleRecipientName = targetUser.UserName
            });
            await _context.SaveChangesAsync();
            return Json(new { response = ResponseMessages.Success });
        }
    }
}
