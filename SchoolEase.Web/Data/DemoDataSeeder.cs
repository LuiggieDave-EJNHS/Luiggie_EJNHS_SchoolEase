using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Data;

/// <summary>
/// Generates the academic structure and people records for the demo school year:
/// grade levels, teaching and non-teaching personnel, sections, subjects,
/// guardians, learners, enrolments, teaching loads and the weekly timetable.
/// The follow-up record data lives in <see cref="DemoRecordsSeeder"/>.
/// </summary>
public static class DemoDataSeeder
{
    private static readonly Random Rng = new(20251020);

    /// <summary>Standard senior-high bell schedule used for the generated timetable.</summary>
    public static readonly (TimeSpan Start, TimeSpan End)[] Periods =
    {
        (new TimeSpan(7, 30, 0), new TimeSpan(8, 30, 0)),
        (new TimeSpan(8, 30, 0), new TimeSpan(9, 30, 0)),
        (new TimeSpan(9, 45, 0), new TimeSpan(10, 45, 0))
    };

    private static readonly string[] FirstNamesMale =
    {
        "Juan Miguel", "Jose Rafael", "Mark Anthony", "John Paul", "Carlo", "Renz Aldrin", "Kevin",
        "Joshua", "Christian Dave", "Angelo", "Paolo", "Jerome", "Vincent", "Reymart", "Dominic",
        "Emmanuel", "Lorenzo", "Gabriel", "Francis", "Adrian"
    };

    private static readonly string[] FirstNamesFemale =
    {
        "Ana Marie", "Maria Sofia", "Kimberly", "Jasmine", "Angelica", "Bea Andrea", "Kristine",
        "Rhea", "Niña", "Patricia", "Shaira", "Chloe", "Hannah", "Lorraine", "Micah",
        "Alyssa", "Sarah Jane", "Danielle", "Erika", "Trisha"
    };

    private static readonly string[] LastNames =
    {
        "Dela Rosa", "Bautista", "Reyes", "Cruz", "Santos", "Aquino", "Mendoza", "Lim", "Garcia",
        "Villanueva", "Fernandez", "Tolentino", "Navarro", "Pascual", "Dizon", "Ramos", "Ong",
        "Manalo", "Villamor", "Aguilar", "Mabini", "Salazar", "Cordero", "Estrella"
    };

    private static readonly string[] Barangays =
    {
        "Sampaloc", "Quiapo", "Sta. Cruz", "Tondo", "Pandacan", "Malate", "Ermita", "San Miguel"
    };

    private static readonly string[] Occupations =
    {
        "Tricycle Driver", "Sari-sari Store Owner", "Call Center Agent", "Barangay Health Worker",
        "Welder", "Public School Teacher", "Delivery Rider", "Homemaker", "Security Guard", "OFW (Dubai)"
    };

    public static async Task SeedAsync(ApplicationDbContext db, SeededAccounts accounts)
    {
        var year = SchoolInfo.SchoolYear;
        var today = DateTime.Today;

        // ---------------------------------------------------------------
        // Grade levels
        // ---------------------------------------------------------------
        var grade11 = new GradeLevel { Name = "Grade 11", Code = "G11", OrderIndex = 11, Description = "Senior High School - Grade 11" };
        var grade12 = new GradeLevel { Name = "Grade 12", Code = "G12", OrderIndex = 12, Description = "Senior High School - Grade 12" };
        db.GradeLevels.AddRange(grade11, grade12);
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // Teaching personnel
        // ---------------------------------------------------------------
        var teacherSeeds = new[]
        {
            new { Emp = "EMP-2020-001", First = "Maria Elena", Middle = "Reyes", Last = "Santos", Dept = "TVL - ICT", Spec = "Computer Systems Servicing, Empowerment Technologies", Email = "teacher@schoolease.ph", Female = true },
            new { Emp = "EMP-2020-002", First = "Rodel", Middle = "Bautista", Last = "Bautista", Dept = "Mathematics", Spec = "General Mathematics, Statistics and Probability", Email = "rodel.bautista@schoolease.ph", Female = false },
            new { Emp = "EMP-2021-003", First = "Angelica", Middle = "Cruz", Last = "Reyes", Dept = "English", Spec = "Oral Communication, Reading and Writing", Email = "angelica.reyes@schoolease.ph", Female = true },
            new { Emp = "EMP-2019-004", First = "Jonathan", Middle = "Lim", Last = "Cruz", Dept = "Science", Spec = "Earth and Life Science, Physical Science", Email = "jonathan.cruz@schoolease.ph", Female = false },
            new { Emp = "EMP-2018-005", First = "Cristina", Middle = "Dizon", Last = "Dela Cruz", Dept = "Accountancy, Business and Management", Spec = "Entrepreneurship, Fundamentals of ABM", Email = "cristina.delacruz@schoolease.ph", Female = true },
            new { Emp = "EMP-2022-006", First = "Michael", Middle = "Garcia", Last = "Aquino", Dept = "TVL - ICT", Spec = "Empowerment Technologies, MIL", Email = "teacher2@schoolease.ph", Female = false },
            new { Emp = "EMP-2021-007", First = "Bernadette", Middle = "Ong", Last = "Lim", Dept = "Research and Filipino", Spec = "Practical Research, Komunikasyon", Email = "bernadette.lim@schoolease.ph", Female = true },
            new { Emp = "EMP-2023-008", First = "Paolo", Middle = "Ramos", Last = "Mendoza", Dept = "Social Science and MAPEH", Spec = "UCSP, Physical Education", Email = "paolo.mendoza@schoolease.ph", Female = false }
        };

        var teachers = teacherSeeds.Select((seed, i) => new Teacher
        {
            EmployeeNumber = seed.Emp,
            FirstName = seed.First,
            MiddleName = seed.Middle,
            LastName = seed.Last,
            Gender = seed.Female ? Gender.Female : Gender.Male,
            BirthDate = new DateTime(1980 + i, (i % 12) + 1, ((i * 3) % 27) + 1),
            Address = $"{100 + i} Mabini Street, Sampaloc, Manila",
            ContactNumber = $"0917{2000000 + i * 13579}",
            Email = seed.Email,
            Department = seed.Dept,
            Specialization = seed.Spec,
            EmploymentStatus = i % 5 == 0 ? EmploymentStatus.Probationary : EmploymentStatus.Permanent,
            DateHired = new DateTime(2018 + (i % 5), (i % 12) + 1, 15),
            IsActive = true
        }).ToList();

        db.Teachers.AddRange(teachers);
        await db.SaveChangesAsync();

        teachers[0].UserId = accounts.Teacher.Id;
        teachers[5].UserId = accounts.Teacher2.Id;
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // Non-teaching personnel
        // ---------------------------------------------------------------
        var staffSeeds = new[]
        {
            new { No = "STF-2017-001", First = "Rosario", Last = "Villanueva", Pos = "School Registrar", Dept = "Office of the Registrar", Email = "staff@schoolease.ph", Female = true },
            new { No = "STF-2015-002", First = "Domingo", Last = "Pascual", Pos = "Administrative Officer II", Dept = "Administration", Email = "registrar@schoolease.ph", Female = false },
            new { No = "STF-2019-003", First = "Liza", Last = "Fernandez", Pos = "Guidance Counselor", Dept = "Guidance and Counseling Office", Email = "liza.fernandez@schoolease.ph", Female = true },
            new { No = "STF-2020-004", First = "Arturo", Last = "Navarro", Pos = "School Librarian", Dept = "Learning Resource Center", Email = "arturo.navarro@schoolease.ph", Female = false },
            new { No = "STF-2022-005", First = "Jasmine", Last = "Tolentino", Pos = "Cashier / Finance Clerk", Dept = "Finance Office", Email = "jasmine.tolentino@schoolease.ph", Female = true }
        };

        var staffMembers = staffSeeds.Select((seed, i) => new Staff
        {
            StaffNumber = seed.No,
            FirstName = seed.First,
            MiddleName = i % 2 == 0 ? "Santos" : "Reyes",
            LastName = seed.Last,
            Gender = seed.Female ? Gender.Female : Gender.Male,
            BirthDate = new DateTime(1982 + i, (i % 12) + 1, ((i * 5) % 27) + 1),
            Address = $"{20 + i} Espana Boulevard, Sampaloc, Manila",
            ContactNumber = $"0918{3000000 + i * 24680}",
            Email = seed.Email,
            Position = seed.Pos,
            Department = seed.Dept,
            EmploymentStatus = EmploymentStatus.Permanent,
            DateHired = new DateTime(2015 + i, (i % 12) + 1, 10),
            IsActive = true
        }).ToList();

        db.StaffMembers.AddRange(staffMembers);
        await db.SaveChangesAsync();

        staffMembers[0].UserId = accounts.Staff.Id;
        staffMembers[1].UserId = accounts.Registrar.Id;
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // Sections
        // ---------------------------------------------------------------
        var sectionSeeds = new (string Name, string Code, string Track, string Room, GradeLevel Level, int Adviser)[]
        {
            ("11-STEM-A", "G11-STEM-A", "Academic - STEM", "Room 201", grade11, 1),
            ("11-ABM-A", "G11-ABM-A", "Academic - ABM", "Room 202", grade11, 4),
            ("11-HUMSS-A", "G11-HUMSS-A", "Academic - HUMSS", "Room 203", grade11, 2),
            ("11-ICT-CCS", "G11-ICT-CCS", "TVL - ICT (CSS NC II)", "Computer Laboratory 1", grade11, 5),
            ("12-STEM-A", "G12-STEM-A", "Academic - STEM", "Room 204", grade12, 3),
            ("12-ABM-A", "G12-ABM-A", "Academic - ABM", "Room 205", grade12, 4),
            ("12-HUMSS-A", "G12-HUMSS-A", "Academic - HUMSS", "Room 206", grade12, 7),
            ("12-ICT-CCS", "G12-ICT-CCS", "TVL - ICT (CSS NC II)", "Computer Laboratory 2", grade12, 0)
        };

        var sections = sectionSeeds.Select(s => new Section
        {
            Name = s.Name,
            Code = s.Code,
            GradeLevelId = s.Level.Id,
            AdviserId = teachers[s.Adviser].Id,
            TrackStrand = s.Track,
            Room = s.Room,
            Capacity = 40,
            SchoolYear = year,
            IsActive = true
        }).ToList();

        db.Sections.AddRange(sections);
        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // Subjects: eight per grade level, mapped 1:1 to the eight teachers so
        // the generated timetable never double-books a teacher.
        // ---------------------------------------------------------------
        var grade11Subjects = new (string Code, string Title, string Track, double Units)[]
        {
            ("ORALCOM", "Oral Communication in Context", "Core", 3),
            ("GENMATH", "General Mathematics", "Core", 3),
            ("EARTHSCI", "Earth and Life Science", "Core", 3),
            ("EMPTECH", "Empowerment Technologies", "Applied", 3),
            ("PERDEV", "Personal Development", "Core", 3),
            ("KOMFIL", "Komunikasyon at Pananaliksik sa Wika at Kulturang Pilipino", "Core", 3),
            ("UCSP", "Understanding Culture, Society and Politics", "Core", 3),
            ("PE1", "Physical Education and Health 1", "Core", 1)
        };

        var grade12Subjects = new (string Code, string Title, string Track, double Units)[]
        {
            ("PR2", "Practical Research 2", "Applied", 3),
            ("MIL", "Media and Information Literacy", "Core", 3),
            ("PHYSCI", "Physical Science", "Core", 3),
            ("ENTREP", "Entrepreneurship", "Applied", 3),
            ("ITCSS", "ICT - Computer Systems Servicing NC II", "TVL - ICT", 3),
            ("RWS", "Reading and Writing Skills", "Core", 3),
            ("PE3", "Physical Education and Health 3", "Core", 1),
            ("STATPROB", "Statistics and Probability", "Core", 3)
        };

        var subjects11 = grade11Subjects.Select(s => new Subject
        {
            Code = s.Code,
            Title = s.Title,
            Description = $"{s.Title} - {s.Track} subject for Grade 11.",
            Units = s.Units,
            GradeLevelId = grade11.Id,
            TrackStrand = s.Track,
            IsActive = true
        }).ToList();

        var subjects12 = grade12Subjects.Select(s => new Subject
        {
            Code = s.Code,
            Title = s.Title,
            Description = $"{s.Title} - {s.Track} subject for Grade 12.",
            Units = s.Units,
            GradeLevelId = grade12.Id,
            TrackStrand = s.Track,
            IsActive = true
        }).ToList();

        db.Subjects.AddRange(subjects11);
        db.Subjects.AddRange(subjects12);
        await db.SaveChangesAsync();

        // Index i of the subject list is taught by teacher i.
        var pairs11 = subjects11.Select((s, i) => (Subject: s, Teacher: teachers[i])).ToList();
        var pairs12 = subjects12.Select((s, i) => (Subject: s, Teacher: teachers[i])).ToList();

        // ---------------------------------------------------------------
        // Guardians and learners
        // ---------------------------------------------------------------
        var guardians = new List<Guardian>();
        var studentCount = sections.Count * 6;

        for (var i = 0; i < studentCount; i++)
        {
            var last = LastNames[(i * 5) % LastNames.Length];
            var barangay = Barangays[i % Barangays.Length];
            var isMale = i % 2 == 0;

            guardians.Add(new Guardian
            {
                FirstName = isMale ? "Rodolfo" : "Marilou",
                MiddleName = "Santos",
                LastName = last,
                Relationship = i % 3 == 0 ? "Father" : "Mother",
                ContactNumber = $"0999{Rng.Next(1000000, 9999999)}",
                Email = $"guardian{i + 1}@example.ph",
                Address = $"{Rng.Next(1, 250)} {barangay} Street, {barangay}, Manila",
                Occupation = Occupations[i % Occupations.Length]
            });
        }

        db.Guardians.AddRange(guardians);
        await db.SaveChangesAsync();

        var students = new List<Student>();
        var counter = 0;
        var studentNumber = 1;

        foreach (var section in sections)
        {
            for (var i = 0; i < 6; i++)
            {
                var guardian = guardians[counter];
                var isMale = counter % 2 == 0;
                var barangay = Barangays[(counter + i) % Barangays.Length];

                var status = counter switch
                {
                    21 => EnrollmentStatus.Pending,
                    34 => EnrollmentStatus.TransferredOut,
                    40 => EnrollmentStatus.Pending,
                    _ => EnrollmentStatus.Enrolled
                };

                students.Add(new Student
                {
                    StudentNumber = $"2025-{studentNumber:D4}",
                    Lrn = $"1{Rng.NextInt64(10000000000L, 99999999999L):D11}",
                    FirstName = isMale
                        ? FirstNamesMale[(counter * 3 + i) % FirstNamesMale.Length]
                        : FirstNamesFemale[(counter * 3 + i) % FirstNamesFemale.Length],
                    MiddleName = LastNames[(counter * 7) % LastNames.Length],
                    LastName = guardian.LastName,
                    Gender = isMale ? Gender.Male : Gender.Female,
                    BirthDate = new DateTime(section.GradeLevelId == grade12.Id ? 2007 : 2008, (counter % 12) + 1, ((counter * 3) % 27) + 1),
                    Address = $"{Rng.Next(1, 250)} {barangay} Street, {barangay}, Manila",
                    ContactNumber = $"0956{Rng.Next(1000000, 9999999)}",
                    Email = $"student{studentNumber:D4}@schoolease.ph",
                    GuardianId = guardian.Id,
                    GradeLevelId = section.GradeLevelId,
                    SectionId = section.Id,
                    EnrollmentStatus = status,
                    DateEnrolled = today.AddDays(-Rng.Next(30, 180)),
                    IsActive = status != EnrollmentStatus.TransferredOut
                });

                studentNumber++;
                counter++;
            }
        }

        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // Attach the two learner demo accounts to the first two records.
        students[0].UserId = accounts.Student.Id;
        students[0].FirstName = "Juan Miguel";
        students[0].LastName = "Dela Rosa";
        students[0].Gender = Gender.Male;

        students[1].UserId = accounts.Student2.Id;
        students[1].FirstName = "Ana Marie";
        students[1].LastName = "Bautista";
        students[1].Gender = Gender.Female;

        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // Enrolment history
        // ---------------------------------------------------------------
        foreach (var student in students)
        {
            db.Enrollments.Add(new Enrollment
            {
                StudentId = student.Id,
                GradeLevelId = student.GradeLevelId!.Value,
                SectionId = student.SectionId,
                SchoolYear = year,
                Status = student.EnrollmentStatus,
                DateEnrolled = student.DateEnrolled,
                Remarks = student.EnrollmentStatus == EnrollmentStatus.Enrolled
                    ? "Complete submission of Form 138 and Certificate of Good Moral."
                    : null
            });
        }

        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // Teaching loads
        // ---------------------------------------------------------------
        foreach (var section in sections)
        {
            var pairs = section.GradeLevelId == grade11.Id ? pairs11 : pairs12;

            foreach (var pair in pairs)
            {
                db.TeacherSubjectAssignments.Add(new TeacherSubjectAssignment
                {
                    TeacherId = pair.Teacher.Id,
                    SubjectId = pair.Subject.Id,
                    SectionId = section.Id,
                    SchoolYear = year
                });
            }
        }

        await db.SaveChangesAsync();

        // ---------------------------------------------------------------
        // Weekly timetable
        // teacherIndex = (section + day * 3 + period) % 8 keeps every period
        // conflict-free per teacher, per section and per room.
        // ---------------------------------------------------------------
        for (var s = 0; s < sections.Count; s++)
        {
            var section = sections[s];
            var pairs = section.GradeLevelId == grade11.Id ? pairs11 : pairs12;

            for (var dayOffset = 0; dayOffset < 5; dayOffset++)
            {
                var day = (DayOfWeek)(dayOffset + 1); // Monday .. Friday

                for (var period = 0; period < Periods.Length; period++)
                {
                    var pair = pairs[(s + dayOffset * 3 + period) % teachers.Count];

                    db.ClassSchedules.Add(new ClassSchedule
                    {
                        SectionId = section.Id,
                        SubjectId = pair.Subject.Id,
                        TeacherId = pair.Teacher.Id,
                        Room = section.Room,
                        DayOfWeek = day,
                        StartTime = Periods[period].Start,
                        EndTime = Periods[period].End,
                        SchoolYear = year
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        await DemoRecordsSeeder.SeedAsync(db, accounts);
    }
}
