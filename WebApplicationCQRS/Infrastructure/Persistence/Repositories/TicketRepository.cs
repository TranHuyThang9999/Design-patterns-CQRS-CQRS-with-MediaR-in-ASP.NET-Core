using Microsoft.EntityFrameworkCore;
using WebApplicationCQRS.Application.DTOs;
using WebApplicationCQRS.Common.Enums;
using WebApplicationCQRS.Domain.Entities;
using WebApplicationCQRS.Domain.Interfaces;
using WebApplicationCQRS.Infrastructure.Persistence.Context;

namespace WebApplicationCQRS.Infrastructure.Persistence.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _context;

    public TicketRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> AddTicket(Ticket ticket)
    {
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();
        return ticket.Id;
    }

    public async Task<List<AssignedTicketDetail>> GetTicketsByCreatorId(int creatorId)
    {
        var assignedTickets = await _context.Tickets
            .Where(t => t.CreatorId == creatorId)
            .GroupJoin(
                _context.AssignedTickets,
                t => t.Id,
                at => at.TicketId,
                (t, atGroup) => new { Ticket = t, Assignments = atGroup }
            )
            .Select(x => new AssignedTicketDetail
            {
                Id = x.Ticket.Id,
                Name = x.Ticket.Name,
                CreatorId = x.Ticket.CreatorId,
                Description = x.Ticket.Description,
                FileDescription = x.Ticket.FileDescription,

                // Người được assign cuối cùng
                AssigneeId = x.Assignments.OrderByDescending(at => at.UpdatedAt)
                    .Select(at => at.AssigneeId)
                    .FirstOrDefault(),
                AssigneeName = x.Assignments.OrderByDescending(at => at.UpdatedAt)
                    .Select(at => _context.Users.FirstOrDefault(u => u.Id == at.AssigneeId).Name)
                    .FirstOrDefault(),

                // Người assign cuối cùng
                AssignerId = x.Assignments.OrderByDescending(at => at.UpdatedAt)
                    .Select(at => at.AssignerId)
                    .FirstOrDefault(),
                AssignerName = x.Assignments.OrderByDescending(at => at.UpdatedAt)
                    .Select(at => _context.Users.FirstOrDefault(u => u.Id == at.AssignerId).Name)
                    .FirstOrDefault(),

                // Lấy người được assign đầu tiên
                FirstAssginId = x.Assignments.OrderBy(at => at.UpdatedAt)
                    .Select(at => at.AssigneeId)
                    .FirstOrDefault(),
                FirstAssginName = x.Assignments.OrderBy(at => at.UpdatedAt)
                    .Select(at => _context.Users
                        .Where(u => u.Id == at.AssigneeId)
                        .Select(u => u.Name)
                        .FirstOrDefault())
                    .FirstOrDefault(),

                // Lấy thời gian assign gần nhất
                AssignedAt = x.Assignments.OrderByDescending(at => at.UpdatedAt)
                    .Select(at => at.UpdatedAt)
                    .FirstOrDefault(),

                Status = x.Assignments.OrderByDescending(at => at.UpdatedAt)
                    .Select(at => at.Status)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        return assignedTickets;
    }


    public async Task UpdateTicket(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();
    }


    public async Task DeleteTicketsById(int[] ids)
    {
        await _context.Tickets.Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync();
    }

    public async Task<Ticket?> GetTicketById(int id)
    {
        return await _context.Tickets.Where(u => u.Id == id).FirstOrDefaultAsync();
    }

    public async Task<bool> CheckListTicketExists(List<int> ids)
    {
        if (ids == null || ids.Count == 0)
            return false;

        var count = await _context.Tickets
            .Where(u => ids.Contains(u.Id))
            .CountAsync();

        return count == ids.Count;
    }

    /// Lấy danh sách ticket mà người dùng hiện tại được assign.
    public async Task<List<ReceivedAssignedTicketDTO>> GetTicketsAssignedToMe(int userId)
    {
        return await _context.AssignedTickets
            .Where(at => at.AssigneeId == userId)
            .Join(
                _context.Tickets,
                at => at.TicketId,
                t => t.Id,
                (at, t) => new { at, t }
            )
            .Join(
                _context.Users,
                at_t => at_t.at.AssignerId,
                u => u.Id,
                (at_t, u) => new ReceivedAssignedTicketDTO
                {
                    Id = at_t.at.Id,
                    AssignedTicketId = at_t.at.Id,
                    TicketId = at_t.t.Id,
                    Name = at_t.t.Name,
                    Description = at_t.t.Description,
                    FileDescription = at_t.t.FileDescription,
                    AssignerId = at_t.at.AssignerId,
                    NameUserAssignerIdTicket = u.Name,
                    TimeAssign = at_t.at.UpdatedAt ?? DateTime.UtcNow,
                }
            )
            .ToListAsync();
    }


    /// Lấy danh sách ticket mà đã assign cho người khác.
    public async Task<List<AssignedTickets>> GetTicketsAssignedByMe(int userId)
    {
        return await _context.AssignedTickets
            .Where(at => at.AssignerId == userId)
            .Join(
                _context.Tickets,
                at => at.TicketId,
                t => t.Id,
                (at, t) => new AssignedTickets
                {
                    Id = t.Id,
                    AssigneeId = at.AssigneeId,
                    Name = t.Name,
                    FileDescription = t.FileDescription,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                }
            )
            .ToListAsync();
    }

    public async Task<bool> CheckIfUserIsCreatorOfTickets(int creatorId, List<int> ticketIds)
    {
        var count = await _context.Tickets
            .Where(t => t.CreatorId == creatorId && ticketIds.Contains(t.Id))
            .CountAsync();

        return count == ticketIds.Count;
    }
    
    public async Task<List<AssignedTicketDetail>> SearchTickets(int userId, string ticketName)
    {
        // Using EF Core's FromSqlRaw to execute raw SQL with LIKE operator
        var query = from t in _context.Tickets
            join at in _context.AssignedTickets on t.Id equals at.TicketId into assignedTickets
            where (t.CreatorId == userId || assignedTickets.Any(at => at.AssigneeId == userId || at.AssignerId == userId))
                  && EF.Functions.Like(t.Name, $"%{ticketName}%")
            select new AssignedTicketDetail
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                FileDescription = t.FileDescription,
                CreatorId = t.CreatorId,

                // Lấy Assignee đầu tiên theo thời gian giao
                FirstAssginId = assignedTickets.OrderBy(at => at.CreatedAt).Select(at => at.AssigneeId).FirstOrDefault(),
                FirstAssginName = assignedTickets.OrderBy(at => at.CreatedAt).Select(at => at.Assignee.Name).FirstOrDefault(),

                // Gộp danh sách Assignee
                AssigneeId = assignedTickets.Select(at => at.AssigneeId).FirstOrDefault(),
                AssigneeName = string.Join(", ", assignedTickets.Select(at => at.Assignee.Name).Distinct()),

                // Gộp danh sách Assigner
                AssignerId = assignedTickets.Select(at => at.AssignerId).FirstOrDefault(),
                AssignerName = string.Join(", ", assignedTickets.Select(at => at.Assigner.Name).Distinct()),

                Status = assignedTickets.OrderByDescending(at => at.UpdatedAt).Select(at => at.Status).FirstOrDefault(),
                AssignedAt = assignedTickets.OrderByDescending(at => at.UpdatedAt).Select(at => at.UpdatedAt).FirstOrDefault()
            };

        return await query.ToListAsync();


    }

}