using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class CourseController : Controller
    {
       
        string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

        // GET: Course/Index
        public ActionResult Index()
        {
            // Boilerplate: Check session (Admin only)
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");
            if (Session["role"].ToString() != "Admin") return RedirectToAction("Dashboard", "Home");

            ViewBag.Title = "Course Management";
            ViewBag.Subtitle = "Manage courses and fee structures per school year";

            // we will use Lists of Dictionaries to pass data without needing Models!
            var courses = new List<Dictionary<string, string>>();
            var fees = new List<Dictionary<string, string>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // fetch Courses
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM tbl_courses ORDER BY course_code", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var c = new Dictionary<string, string>();
                        c["course_id"] = reader["course_id"].ToString();
                        c["course_code"] = reader["course_code"].ToString();
                        c["course_name"] = reader["course_name"].ToString();
                        c["is_active"] = reader["is_active"].ToString();
                        courses.Add(c);
                    }
                }

                // fetch Course Fees (Joined with Courses to get the Course Code)
                string feeSql = @"SELECT f.*, c.course_code 
                                  FROM tbl_course_fees f 
                                  JOIN tbl_courses c ON f.course_id = c.course_id 
                                  ORDER BY f.school_year DESC, c.course_code";

                using (SqlCommand cmd = new SqlCommand(feeSql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var f = new Dictionary<string, string>();
                        f["fee_id"] = reader["fee_id"].ToString();
                        f["course_code"] = reader["course_code"].ToString();
                        f["school_year"] = reader["school_year"].ToString();
                        f["tuition_per_unit"] = Convert.ToDecimal(reader["tuition_per_unit"]).ToString("N2");
                        f["registration_fee"] = Convert.ToDecimal(reader["registration_fee"]).ToString("N2");
                        f["miscellaneous_fee"] = Convert.ToDecimal(reader["miscellaneous_fee"]).ToString("N2");
                        f["laboratory_fee"] = Convert.ToDecimal(reader["laboratory_fee"]).ToString("N2");
                        fees.Add(f);
                    }
                }
            }

            ViewBag.Courses = courses;
            ViewBag.Fees = fees;

            return View();
        }

        // POST: Save Course (AJAX)
        [HttpPost]
        public JsonResult SaveCourse(string courseCode, string courseName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "INSERT INTO tbl_courses (course_code, course_name) VALUES (@code, @name)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@code", courseCode.ToUpper());
                        cmd.Parameters.AddWithValue("@name", courseName);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { status = "ok", message = "Course added successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }

        // POST: Save Fee Rate (AJAX)
        [HttpPost]
        public JsonResult SaveFee(int courseId, string schoolYear, decimal tuition, decimal reg, decimal misc, decimal lab)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string sql = @"INSERT INTO tbl_course_fees 
                                  (course_id, school_year, tuition_per_unit, registration_fee, miscellaneous_fee, laboratory_fee) 
                                  VALUES (@cid, @sy, @tuit, @reg, @misc, @lab)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@cid", courseId);
                        cmd.Parameters.AddWithValue("@sy", schoolYear);
                        cmd.Parameters.AddWithValue("@tuit", tuition);
                        cmd.Parameters.AddWithValue("@reg", reg);
                        cmd.Parameters.AddWithValue("@misc", misc);
                        cmd.Parameters.AddWithValue("@lab", lab);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { status = "ok", message = "Fee rate added successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
    }
}