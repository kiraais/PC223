using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class HomeController : Controller
    {
        string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

        public ActionResult Dashboard()
        {
            if (Session["userid"] == null)
                return RedirectToAction("Login", "Auth");

            string role = Session["role"].ToString();
            string userId = Session["userid"].ToString();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                if (role == "Admin")
                {
                    ViewBag.Title = "Admin Dashboard";
                    ViewBag.Subtitle = "System overview and live metrics";

                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM tbl_students WHERE status = 'Enrolled'", conn))
                        ViewBag.TotalStudents = (int)cmd.ExecuteScalar();

                    using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(total_assessment), 0) FROM tbl_assessments", conn))
                        ViewBag.TotalAssessed = Convert.ToDecimal(cmd.ExecuteScalar()).ToString("N2");

                    using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(amount_paid), 0) FROM tbl_payments", conn))
                        ViewBag.TotalCollected = Convert.ToDecimal(cmd.ExecuteScalar()).ToString("N2");

                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM tbl_students WHERE scholarship_id IS NOT NULL", conn))
                        ViewBag.TotalScholarships = (int)cmd.ExecuteScalar();

                    decimal assess = Convert.ToDecimal(ViewBag.TotalAssessed);
                    decimal coll = Convert.ToDecimal(ViewBag.TotalCollected);

                    ViewBag.OutstandingBalance = (assess - coll).ToString("N2");
                    ViewBag.CollectionRate = assess > 0 ? ((coll / assess) * 100).ToString("0.0") : "0.0";

                    // Recent Payments
                    var recent = new List<Dictionary<string, string>>();
                    string sqlR = @"SELECT TOP 5 p.*, s.fname, s.lname, s.student_number 
                                    FROM tbl_payments p 
                                    JOIN tbl_students s ON p.student_id = s.student_id 
                                    ORDER BY p.payment_date DESC";

                    using (SqlCommand cmd = new SqlCommand(sqlR, conn))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var dict = new Dictionary<string, string>();
                            dict["name"] = r["fname"] + " " + r["lname"];
                            dict["student_number"] = r["student_number"].ToString();
                            dict["amount"] = Convert.ToDecimal(r["amount_paid"]).ToString("N2");
                            dict["term"] = r["payment_term"].ToString();
                            dict["date"] = Convert.ToDateTime(r["payment_date"]).ToString("MMM dd");
                            recent.Add(dict);
                        }
                    }
                    ViewBag.RecentPayments = recent;

                    // Courses
                    var courses = new List<Dictionary<string, string>>();
                    string sqlC = @"SELECT c.course_code, COUNT(s.student_id) as student_count 
                                    FROM tbl_courses c 
                                    LEFT JOIN tbl_students s 
                                        ON c.course_id = s.course_id AND s.status = 'Enrolled' 
                                    WHERE c.is_active = 1 
                                    GROUP BY c.course_code";

                    using (SqlCommand cmd = new SqlCommand(sqlC, conn))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var c = new Dictionary<string, string>();
                            c["code"] = r["course_code"].ToString();
                            c["count"] = r["student_count"].ToString();
                            courses.Add(c);
                        }
                    }
                    ViewBag.Courses = courses;
                }
                else
                {
                    ViewBag.Title = "Student Portal";
                    ViewBag.Subtitle = "Welcome back, " + Session["firstname"];

                    int studentId = 0;

                    using (SqlCommand cmd = new SqlCommand("SELECT student_id FROM tbl_users WHERE user_id = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        studentId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (SqlCommand cmd = new SqlCommand(@"SELECT s.units_enrolled, s.year_level, c.course_code 
                                                            FROM tbl_students s 
                                                            JOIN tbl_courses c ON s.course_id = c.course_id 
                                                            WHERE s.student_id = @sid", conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                ViewBag.Course = r["course_code"] + " - Yr " + r["year_level"];
                                ViewBag.Units = r["units_enrolled"].ToString();
                            }
                        }
                    }

                    decimal myAssess = 0, myPaid = 0;

                    using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(total_assessment), 0) FROM tbl_assessments WHERE student_id = @sid", conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        myAssess = Convert.ToDecimal(cmd.ExecuteScalar());
                    }

                    using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(amount_paid), 0) FROM tbl_payments WHERE student_id = @sid", conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        myPaid = Convert.ToDecimal(cmd.ExecuteScalar());
                    }

                    ViewBag.MyAssessed = myAssess.ToString("N2");
                    ViewBag.MyBalance = (myAssess - myPaid).ToString("N2");

                    var myPayments = new List<Dictionary<string, string>>();

                    using (SqlCommand cmd = new SqlCommand("SELECT TOP 5 * FROM tbl_payments WHERE student_id = @sid ORDER BY payment_date DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var p = new Dictionary<string, string>();
                                p["amount"] = Convert.ToDecimal(r["amount_paid"]).ToString("N2");
                                p["term"] = r["payment_term"].ToString();
                                p["date"] = Convert.ToDateTime(r["payment_date"]).ToString("MMM dd, yyyy");
                                myPayments.Add(p);
                            }
                        }
                    }
                    ViewBag.MyPayments = myPayments;
                }
            }

            return View();
        }
    }
}