using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class StudentController : Controller
    {
        string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

        // GET: Student
        public ActionResult Index()
        {
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");
            if (Session["role"].ToString() != "Admin") return RedirectToAction("Dashboard", "Home");

            ViewBag.Title = "Student Accounts";
            ViewBag.Subtitle = "Manage and register enrolled students";

            var students = new List<Dictionary<string, string>>();
            var courses = new List<Dictionary<string, string>>();
            var scholarships = new List<Dictionary<string, string>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // fetch Students (Joined with Courses)
                string sqlStudents = @"SELECT s.*, c.course_code 
                                       FROM tbl_students s 
                                       LEFT JOIN tbl_courses c ON s.course_id = c.course_id 
                                       ORDER BY s.lname, s.fname";
                using (SqlCommand cmd = new SqlCommand(sqlStudents, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var s = new Dictionary<string, string>();
                        s["id"] = reader["student_id"].ToString();
                        s["student_number"] = reader["student_number"].ToString();
                        s["fullname"] = reader["lname"].ToString() + ", " + reader["fname"].ToString();
                        s["email"] = reader["email"].ToString();
                        s["course_code"] = reader["course_code"].ToString();
                        s["year_level"] = reader["year_level"].ToString();
                        s["status"] = reader["status"].ToString();
                        students.Add(s);
                    }
                }

                // fetch active courses for the dropdown
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM tbl_courses WHERE is_active=1", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var c = new Dictionary<string, string>();
                        c["id"] = reader["course_id"].ToString();
                        c["code"] = reader["course_code"].ToString();
                        courses.Add(c);
                    }
                }

                // fetch active scholarships for the dropdown
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM tbl_scholarships WHERE is_active=1", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var sch = new Dictionary<string, string>();
                        sch["id"] = reader["scholarship_id"].ToString();
                        sch["name"] = reader["scholarship_name"].ToString();
                        scholarships.Add(sch);
                    }
                }
            }

            ViewBag.Students = students;
            ViewBag.Courses = courses;
            ViewBag.Scholarships = scholarships;

            return View();
        }

        // POST: Register Student
        [HttpPost]
        public JsonResult Save(string studentNo, string fname, string mname, string lname, string gender,
                               string bdate, string address, string contact, string email,
                               int courseId, int yearLevel, string schoolYear, int units, string scholId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    // transaction para if ma quit kay dili ra isave sa db so ma create sya safely
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // ins. student sa db
                            string sqlStudent = @"INSERT INTO tbl_students 
                                (student_number, fname, mname, lname, gender, birthdate, address, contact_number, email, 
                                 course_id, year_level, school_year, units_enrolled, scholarship_id, status) 
                                OUTPUT INSERTED.student_id 
                                VALUES (@sno, @fn, @mn, @ln, @gen, @dob, @add, @con, @em, @cid, @yl, @sy, @units, @schid, 'Enrolled')";

                            int newStudentId = 0;
                            using (SqlCommand cmd = new SqlCommand(sqlStudent, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@sno", studentNo);
                                cmd.Parameters.AddWithValue("@fn", fname);
                                cmd.Parameters.AddWithValue("@mn", string.IsNullOrEmpty(mname) ? (object)DBNull.Value : mname);
                                cmd.Parameters.AddWithValue("@ln", lname);
                                cmd.Parameters.AddWithValue("@gen", gender);
                                cmd.Parameters.AddWithValue("@dob", string.IsNullOrEmpty(bdate) ? (object)DBNull.Value : Convert.ToDateTime(bdate));
                                cmd.Parameters.AddWithValue("@add", address);
                                cmd.Parameters.AddWithValue("@con", contact);
                                cmd.Parameters.AddWithValue("@em", email);
                                cmd.Parameters.AddWithValue("@cid", courseId);
                                cmd.Parameters.AddWithValue("@yl", yearLevel);
                                cmd.Parameters.AddWithValue("@sy", schoolYear);
                                cmd.Parameters.AddWithValue("@units", units);
                                cmd.Parameters.AddWithValue("@schid", string.IsNullOrEmpty(scholId) ? (object)DBNull.Value : Convert.ToInt32(scholId));

                                newStudentId = (int)cmd.ExecuteScalar(); // kuhaon ang newly created student_id
                            }

                            // insert User Login password default kay ang student number
                            string sqlUser = "INSERT INTO tbl_users (username, password, role, student_id) VALUES (@user, @pass, 'Student', @sid)";
                            using (SqlCommand cmdU = new SqlCommand(sqlUser, conn, trans))
                            {
                                cmdU.Parameters.AddWithValue("@user", studentNo);
                                cmdU.Parameters.AddWithValue("@pass", studentNo); // Default password
                                cmdU.Parameters.AddWithValue("@sid", newStudentId);
                                cmdU.ExecuteNonQuery();
                            }

                            trans.Commit(); // Save both!
                            return Json(new { status = "ok", message = "Student registered and user account created." });
                        }
                        catch (Exception ex)
                        {
                            trans.Rollback(); // If anything fails, revert everything
                            throw new Exception("Transaction Failed: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
    }
}