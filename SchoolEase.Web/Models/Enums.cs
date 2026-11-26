using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models;

/// <summary>Sex of a person record.</summary>
public enum Gender
{
    [Display(Name = "Male")] Male = 1,
    [Display(Name = "Female")] Female = 2
}

/// <summary>Enrollment lifecycle of a student.</summary>
public enum EnrollmentStatus
{
    [Display(Name = "Pending")] Pending = 0,
    [Display(Name = "Enrolled")] Enrolled = 1,
    [Display(Name = "Dropped")] Dropped = 2,
    [Display(Name = "Transferred Out")] TransferredOut = 3,
    [Display(Name = "Graduated")] Graduated = 4
}

/// <summary>Employment status of teaching and non-teaching personnel.</summary>
public enum EmploymentStatus
{
    [Display(Name = "Probationary")] Probationary = 0,
    [Display(Name = "Permanent")] Permanent = 1,
    [Display(Name = "Contractual")] Contractual = 2,
    [Display(Name = "Part-Time")] PartTime = 3,
    [Display(Name = "Resigned")] Resigned = 4,
    [Display(Name = "Retired")] Retired = 5
}

/// <summary>Daily attendance marking.</summary>
public enum AttendanceStatus
{
    [Display(Name = "Present")] Present = 1,
    [Display(Name = "Late")] Late = 2,
    [Display(Name = "Absent")] Absent = 3,
    [Display(Name = "Excused")] Excused = 4
}

/// <summary>Workflow status of a document request.</summary>
public enum DocumentRequestStatus
{
    [Display(Name = "Pending")] Pending = 1,
    [Display(Name = "Processing")] Processing = 2,
    [Display(Name = "Ready for Release")] ReadyForRelease = 3,
    [Display(Name = "Released")] Released = 4,
    [Display(Name = "Rejected")] Rejected = 5
}

/// <summary>Kind of record kept in a learner's 201 file.</summary>
public enum StudentDocumentCategory
{
    [Display(Name = "Birth Certificate")] BirthCertificate = 1,
    [Display(Name = "Form 137 (Permanent Record)")] Form137 = 2,
    [Display(Name = "Form 138 (Report Card)")] Form138 = 3,
    [Display(Name = "Good Moral Certificate")] GoodMoral = 4,
    [Display(Name = "Medical / Health Record")] MedicalRecord = 5,
    [Display(Name = "Certificate of Enrollment")] CertificateOfEnrollment = 6,
    [Display(Name = "ID / Photo")] Identification = 7,
    [Display(Name = "Other")] Other = 99
}

/// <summary>Importance of an announcement.</summary>
public enum AnnouncementPriority
{
    [Display(Name = "Low")] Low = 1,
    [Display(Name = "Normal")] Normal = 2,
    [Display(Name = "High")] High = 3,
    [Display(Name = "Urgent")] Urgent = 4
}

/// <summary>Intended readers of an announcement or event.</summary>
public enum Audience
{
    [Display(Name = "Everyone")] All = 0,
    [Display(Name = "Students")] Students = 1,
    [Display(Name = "Teachers")] Teachers = 2,
    [Display(Name = "Staff")] Staff = 3,
    [Display(Name = "Administrators")] Administrators = 4
}

/// <summary>Calendar entry classification.</summary>
public enum EventCategory
{
    [Display(Name = "School Event")] Event = 1,
    [Display(Name = "Holiday")] Holiday = 2,
    [Display(Name = "Examination")] Exam = 3,
    [Display(Name = "Meeting")] Meeting = 4,
    [Display(Name = "Activity")] Activity = 5,
    [Display(Name = "Deadline")] Deadline = 6
}

/// <summary>Visual tone of an in-app notification or toast.</summary>
public enum NotificationType
{
    [Display(Name = "Information")] Info = 1,
    [Display(Name = "Success")] Success = 2,
    [Display(Name = "Warning")] Warning = 3,
    [Display(Name = "Alert")] Danger = 4
}

/// <summary>Why a learner is meeting school staff.</summary>
public enum AppointmentPurpose
{
    [Display(Name = "Registrar / Records")] Registrar = 1,
    [Display(Name = "Document Claiming")] DocumentClaim = 2,
    [Display(Name = "Guidance & Counselling")] Guidance = 3,
    [Display(Name = "Academic Consultation")] Academic = 4,
    [Display(Name = "Finance / Payments")] Finance = 5,
    [Display(Name = "Enrollment")] Enrollment = 6,
    [Display(Name = "Other")] Other = 99
}

/// <summary>Lifecycle of a booked appointment.</summary>
public enum AppointmentStatus
{
    [Display(Name = "Requested")] Requested = 5,
    [Display(Name = "Booked")] Booked = 1,
    [Display(Name = "Completed")] Completed = 2,
    [Display(Name = "Cancelled")] Cancelled = 3,
    [Display(Name = "No Show")] NoShow = 4
}
