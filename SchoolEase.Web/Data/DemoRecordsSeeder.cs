using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Data;

/// <summary>
/// Generates the transactional demo records: attendance, grades, document types
/// and requests, announcements, notifications, calendar entries and an initial
/// audit trail. Runs after <see cref="DemoDataSeeder"/>.
/// </summary>
public static class DemoRecordsSeeder
{
    private static readonly Random Rng = new(20251020);

    public static async Task SeedAsync(ApplicationDbContext db, SeededAccounts accounts)
    {
        var year = SchoolInfo.SchoolYear;
        var today = DateTime.Today;

        var students = await db.Students.Include(s => s.Section).ToListAsync();
        var sections = await db.Sections.ToListAsync();
        var subjects = await db.Subjects.ToListAsync();
        var teachers = await db.Teachers.ToListAsync();

        // ==============================================================
        // Attendance - the last ten school days
        // ==============================================================
        var schoolDays = Enumerable.Range(0, 20)
            .Select(offset => today.AddDays(-offset))
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Take(10)
            .Reverse()
            .ToList();

        foreach (var day in schoolDays)
        {
            foreach (var student in students.Where(s => s.SectionId is not null && s.IsActive))
            {
                var roll = Rng.Next(0, 100);
                var status = roll switch
                {
                    < 88 => AttendanceStatus.Present,
                    < 94 => AttendanceStatus.Late,
                    < 98 => AttendanceStatus.Absent,
                    _ => AttendanceStatus.Excused
                };

                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    StudentId = student.Id,
                    SectionId = student.SectionId!.Value,
                    Date = day,
                    Status = status,
                    Remarks = status switch
                    {
                        AttendanceStatus.Late => $"Arrived at {Rng.Next(8, 10)}:{Rng.Next(10, 59):D2} AM",
                        AttendanceStatus.Absent => Rng.Next(0, 2) == 0 ? "No excuse letter submitted" : "Sick leave",
                        AttendanceStatus.Excused => "Excuse letter signed by the guardian",
                        _ => null
                    },
                    RecordedByUserId = accounts.Teacher.Id,
                    RecordedAt = day.AddHours(7).AddMinutes(45)
                });
            }
        }

        await db.SaveChangesAsync();

        // ==============================================================
        // Grades - terms 1 to 3 for three subjects per section
        // ==============================================================
        foreach (var section in sections)
        {
            var sectionStudents = students.Where(s => s.SectionId == section.Id).ToList();
            var gradeLevelId = section.GradeLevelId;
            var sectionSubjects = subjects.Where(s => s.GradeLevelId == gradeLevelId).Take(3).ToList();

            foreach (var subject in sectionSubjects)
            {
                var teacher = await db.TeacherSubjectAssignments
                    .Include(a => a.Teacher)
                    .Where(a => a.SectionId == section.Id && a.SubjectId == subject.Id)
                    .Select(a => a.Teacher)
                    .FirstOrDefaultAsync();

                foreach (var student in sectionStudents)
                {
                    foreach (var term in new[] { 1, 2, 3 })
                    {
                        var baseScore = 78 + Rng.Next(0, 18);

                        var record = new GradeRecord
                        {
                            StudentId = student.Id,
                            SubjectId = subject.Id,
                            SectionId = section.Id,
                            TeacherId = teacher?.Id,
                            SchoolYear = year,
                            Term = term,
                            WrittenWork = Math.Min(100, baseScore + Rng.Next(-3, 4)),
                            PerformanceTask = Math.Min(100, baseScore + Rng.Next(-4, 5)),
                            TermAssessment = Math.Min(100, baseScore + Rng.Next(-5, 6)),
                            EncodedByUserId = teacher?.UserId,
                            EncodedAt = DateTime.UtcNow.AddDays(-Rng.Next(1, 40))
                        };

                        record.FinalGrade = record.ComputeWeighted();
                        record.Remarks = record.FinalGrade >= 75 ? "Passed" : "Failed - for remediation";

                        db.GradeRecords.Add(record);
                    }
                }
            }
        }

        await db.SaveChangesAsync();

        // ==============================================================
        // Document types
        // ==============================================================
        var documentTypes = new List<DocumentType>
        {
            new() { Name = "Certificate of Enrollment", Code = "COE", Description = "Proof that the learner is currently enrolled for the school year.", ProcessingDays = 2 },
            new() { Name = "Certificate of Good Moral Character", Code = "CGMC", Description = "Certifies the learner's good conduct inside and outside the campus.", ProcessingDays = 2 },
            new() { Name = "Transcript of Records (SHS)", Code = "TOR", Description = "Official academic record covering all terms of Senior High School.", ProcessingDays = 5 },
            new() { Name = "Form 137 / SF10 Copy", Code = "SF10", Description = "Permanent record copy of the learner.", ProcessingDays = 4 },
            new() { Name = "Certificate of Graduation", Code = "COG", Description = "Certificate issued to confirmed graduating learners.", ProcessingDays = 3 },
            new() { Name = "Certification of Grades", Code = "CGRADE", Description = "Summary of grades for a specific term.", ProcessingDays = 3 },
            new() { Name = "Certificate of Completion", Code = "COCOMP", Description = "Issued after completing all academic requirements.", ProcessingDays = 3 },
            new() { Name = "Barangay / Scholarship Certification", Code = "BRGY", Description = "Supporting document for scholarship and subsidy applications.", ProcessingDays = 2 }
        };

        db.DocumentTypes.AddRange(documentTypes);
        await db.SaveChangesAsync();

        // ==============================================================
        // Document requests
        // ==============================================================
        var purposes = new[]
        {
            "Scholarship application", "College admission requirement", "Employment requirement",
            "Bank account opening", "Visa application", "TESDA assessment requirement", "Personal file copy"
        };

        var statuses = new[]
        {
            DocumentRequestStatus.Released, DocumentRequestStatus.Released, DocumentRequestStatus.ReadyForRelease,
            DocumentRequestStatus.Processing, DocumentRequestStatus.Pending, DocumentRequestStatus.Pending,
            DocumentRequestStatus.Pending, DocumentRequestStatus.Rejected, DocumentRequestStatus.Processing,
            DocumentRequestStatus.Released, DocumentRequestStatus.ReadyForRelease, DocumentRequestStatus.Pending,
            DocumentRequestStatus.Processing, DocumentRequestStatus.Released, DocumentRequestStatus.Pending,
            DocumentRequestStatus.ReadyForRelease
        };

        for (var i = 0; i < statuses.Length; i++)
        {
            var student = students[(i * 3) % students.Count];
            var type = documentTypes[i % documentTypes.Count];
            var status = statuses[i];
            var requestedAt = DateTime.Now.AddDays(-Rng.Next(1, 60)).AddHours(-Rng.Next(0, 8));

            var request = new DocumentRequest
            {
                RequestNumber = $"REQ-2025-{i + 1:D4}",
                StudentId = student.Id,
                DocumentTypeId = type.Id,
                Purpose = purposes[i % purposes.Length],
                Copies = Rng.Next(1, 4),
                Status = status,
                RequestedAt = requestedAt,
                Remarks = status switch
                {
                    DocumentRequestStatus.Rejected => "Outstanding balance with the finance office.",
                    DocumentRequestStatus.Released => "Claimed by the learner at the registrar's window.",
                    DocumentRequestStatus.ReadyForRelease => "Ready for pick-up at the registrar's office.",
                    DocumentRequestStatus.Processing => "Being prepared by the registrar.",
                    _ => null
                }
            };

            if (status is DocumentRequestStatus.Processing or DocumentRequestStatus.ReadyForRelease
                or DocumentRequestStatus.Released or DocumentRequestStatus.Rejected)
            {
                request.ProcessedByUserId = accounts.Admin.Id;
                request.ProcessedAt = requestedAt.AddHours(Rng.Next(1, 24));
            }

            if (status == DocumentRequestStatus.Released)
            {
                request.ReleasedAt = request.ProcessedAt?.AddDays(1).AddHours(2);
            }

            db.DocumentRequests.Add(request);
        }

        await db.SaveChangesAsync();

        // ==============================================================
        // Announcements
        // ==============================================================
        db.Announcements.AddRange(
            new Announcement
            {
                Title = "Enrollment for the Second Term is Now Open",
                Content = "The Office of the Registrar is now accepting enrollees for the Second Term of S.Y. 2025-2026. Learners must submit their report card and a signed enrollment form. Walk-in enrollment is available from 8:00 AM to 4:00 PM, Monday to Friday.",
                AuthorUserId = accounts.Admin.Id, AuthorName = accounts.Admin.FullName,
                Priority = AnnouncementPriority.High, Audience = Audience.All,
                PublishDate = DateTime.Now.AddDays(-2)
            },
            new Announcement
            {
                Title = "First Term Examinations Schedule",
                Content = "Term examinations will be conducted from Monday to Wednesday next week. Please review the posted examination matrix per grade level. Learners must bring their own pens and calculators; mobile phones are not allowed inside the examination rooms.",
                AuthorUserId = accounts.Admin.Id, AuthorName = accounts.Admin.FullName,
                Priority = AnnouncementPriority.Urgent, Audience = Audience.All,
                PublishDate = DateTime.Now.AddDays(-5)
            },
            new Announcement
            {
                Title = "Faculty Meeting - Encoding of Term Grades",
                Content = "All subject teachers are required to attend the faculty meeting regarding the encoding of term grades in SchoolEase. Please bring your class records and item analysis per section.",
                AuthorUserId = accounts.Admin.Id, AuthorName = accounts.Admin.FullName,
                Priority = AnnouncementPriority.High, Audience = Audience.Teachers,
                PublishDate = DateTime.Now.AddDays(-3)
            },
            new Announcement
            {
                Title = "ICT Week: Coding Competition",
                Content = "The TVL-ICT strand will host a Coding Competition open to Grades 11 and 12. Interested learners may register with their advisers. Prizes and certificates await the winners.",
                AuthorUserId = accounts.Teacher.Id, AuthorName = accounts.Teacher.FullName,
                Priority = AnnouncementPriority.Normal, Audience = Audience.Students,
                PublishDate = DateTime.Now.AddDays(-8)
            },
            new Announcement
            {
                Title = "Document Requests: Faster Processing",
                Content = "Learners may now file document requests online through the Document Requests module. Track your request status and receive a notification once the document is ready for release.",
                AuthorUserId = accounts.Admin.Id, AuthorName = accounts.Admin.FullName,
                Priority = AnnouncementPriority.Normal, Audience = Audience.Students,
                PublishDate = DateTime.Now.AddDays(-12)
            },
            new Announcement
            {
                Title = "Office Hours Reminder for Non-Teaching Personnel",
                Content = "All office personnel are reminded to update the daily logbook and to keep their attendance records current in the system.",
                AuthorUserId = accounts.SuperAdmin.Id, AuthorName = accounts.SuperAdmin.FullName,
                Priority = AnnouncementPriority.Low, Audience = Audience.Staff,
                PublishDate = DateTime.Now.AddDays(-15)
            },
            new Announcement
            {
                Title = "System Maintenance Notice",
                Content = "SchoolEase will undergo scheduled maintenance on Saturday from 10:00 PM to 12:00 MN. The system will be temporarily unavailable during this window.",
                AuthorUserId = accounts.SuperAdmin.Id, AuthorName = accounts.SuperAdmin.FullName,
                Priority = AnnouncementPriority.High, Audience = Audience.All,
                PublishDate = DateTime.Now.AddDays(-1)
            });

        // ==============================================================
        // Notifications
        // ==============================================================
        db.Notifications.AddRange(
            new Notification { UserId = accounts.Admin.Id, Title = "New document request", Message = "A learner submitted a request for Certificate of Good Moral Character.", Type = NotificationType.Info, Url = "/DocumentRequests", CreatedAt = DateTime.UtcNow.AddMinutes(-35) },
            new Notification { UserId = accounts.Admin.Id, Title = "Attendance reminder", Message = "3 sections have no attendance marks recorded today.", Type = NotificationType.Warning, Url = "/Attendance", CreatedAt = DateTime.UtcNow.AddHours(-3) },
            new Notification { UserId = accounts.Admin.Id, Title = "Request ready for release", Message = "REQ-2025-0003 is now ready for release at the registrar.", Type = NotificationType.Success, Url = "/DocumentRequests", CreatedAt = DateTime.UtcNow.AddHours(-6), IsRead = true },
            new Notification { UserId = accounts.Teacher.Id, Title = "New announcement", Message = "Faculty Meeting - Encoding of Term Grades has been posted.", Type = NotificationType.Info, Url = "/Announcements", CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new Notification { UserId = accounts.Teacher.Id, Title = "Attendance reminder", Message = "You have classes today without attendance marks yet.", Type = NotificationType.Warning, Url = "/Attendance", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new Notification { UserId = accounts.Student.Id, Title = "Grade update", Message = "Your Term 1 grades have been encoded.", Type = NotificationType.Info, Url = "/Student/Grades", CreatedAt = DateTime.UtcNow.AddHours(-4) },
            new Notification { UserId = accounts.Student.Id, Title = "Request status update", Message = "Your request for Certificate of Enrollment is now ready for release.", Type = NotificationType.Success, Url = "/Student/Requests", CreatedAt = DateTime.UtcNow.AddDays(-1), IsRead = true },
            new Notification { UserId = accounts.Staff.Id, Title = "Welcome to SchoolEase", Message = "Your staff account is ready. You may now manage document requests.", Type = NotificationType.Success, Url = "/Dashboard", CreatedAt = DateTime.UtcNow.AddDays(-2), IsRead = true });

        // ==============================================================
        // School calendar
        // ==============================================================
        db.SchoolEvents.AddRange(
            new SchoolEvent { Title = "First Term Examinations", Description = "Term assessment for all grade levels.", StartDate = today.AddDays(7), EndDate = today.AddDays(9), Category = EventCategory.Exam, Location = "Respective classrooms" },
            new SchoolEvent { Title = "Buwan ng Wika Culminating Activity", Description = "Cultural presentation and Filipino literary competition.", StartDate = today.AddDays(14), Category = EventCategory.Activity, Location = "School Gymnasium" },
            new SchoolEvent { Title = "Parent-Teacher Conference", Description = "Distribution of report cards and consultation with parents and guardians.", StartDate = today.AddDays(21), Category = EventCategory.Meeting, Location = "Respective classrooms" },
            new SchoolEvent { Title = "ICT Week", Description = "Technology showcase and skills olympics for the TVL-ICT strand.", StartDate = today.AddDays(25), EndDate = today.AddDays(29), Category = EventCategory.Event, Location = "Computer Laboratories" },
            new SchoolEvent { Title = "Bonifacio Day", Description = "Regular holiday nationwide.", StartDate = new DateTime(today.Year, 11, 30), Category = EventCategory.Holiday, Location = "Nationwide" },
            new SchoolEvent { Title = "Work Immersion Orientation for Grade 12", Description = "Pre-deployment orientation for partner establishments.", StartDate = today.AddDays(35), Category = EventCategory.Meeting, Audience = Audience.Students, Location = "Audio-Visual Room" },
            new SchoolEvent { Title = "Deadline for Submission of Grades", Description = "Final date for teachers to encode term grades.", StartDate = today.AddDays(18), Category = EventCategory.Deadline, Audience = Audience.Teachers },
            new SchoolEvent { Title = "Recollection and Values Formation", Description = "Grade 12 values formation activity.", StartDate = today.AddDays(42), Category = EventCategory.Activity, Audience = Audience.Students, Location = "School Chapel" },
            new SchoolEvent { Title = "Christmas Program and Gift-Giving", Description = "Year-end celebration for learners and personnel.", StartDate = new DateTime(today.Year, 12, 18), Category = EventCategory.Event, Location = "School Gymnasium" },
            new SchoolEvent { Title = "Term Break", Description = "No classes - term break.", StartDate = new DateTime(today.Year, 12, 20), EndDate = new DateTime(today.Year + 1, 1, 4), Category = EventCategory.Holiday, Location = "Nationwide" });

        // ==============================================================
        // Initial audit trail
        // ==============================================================
        db.AuditLogs.AddRange(
            new AuditLog { Action = "System Seeded", EntityName = "Database", Details = "Demo dataset generated for S.Y. 2025-2026.", UserName = "System", CreatedAt = DateTime.UtcNow.AddMinutes(-20) },
            new AuditLog { Action = "Created Students", EntityName = "Student", EntityId = students.Count.ToString(), Details = $"Imported {students.Count} learner records.", UserName = accounts.Admin.FullName, UserId = accounts.Admin.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-18) },
            new AuditLog { Action = "Created Sections", EntityName = "Section", EntityId = sections.Count.ToString(), Details = "Enrolled sections for the current school year.", UserName = accounts.Admin.FullName, UserId = accounts.Admin.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-16) },
            new AuditLog { Action = "Updated Schedule", EntityName = "ClassSchedule", Details = "Weekly timetable generated for all sections.", UserName = accounts.Admin.FullName, UserId = accounts.Admin.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-14) },
            new AuditLog { Action = "Processed Request", EntityName = "DocumentRequest", EntityId = "3", Details = "Status changed to Ready for Release.", UserName = accounts.Admin.FullName, UserId = accounts.Admin.Id, CreatedAt = DateTime.UtcNow.AddHours(-6) },
            new AuditLog { Action = "Posted Announcement", EntityName = "Announcement", Details = "System Maintenance Notice published.", UserName = accounts.SuperAdmin.FullName, UserId = accounts.SuperAdmin.Id, CreatedAt = DateTime.UtcNow.AddHours(-20) },
            new AuditLog { Action = "Encoded Grades", EntityName = "GradeRecord", Details = "Term 1 grades encoded for 12-ICT-CCS.", UserName = accounts.Teacher.FullName, UserId = accounts.Teacher.Id, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new AuditLog { Action = "User Login", EntityName = "Account", Details = "Signed in from the registrar's workstation.", UserName = accounts.Staff.FullName, UserId = accounts.Staff.Id, CreatedAt = DateTime.UtcNow.AddDays(-1) });

        await db.SaveChangesAsync();
    }
}
