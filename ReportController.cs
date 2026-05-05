using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class ReportController : Controller
    {
        string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

        // GET: Report
        public ActionResult Index()
        {
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");
            if (Session["role"].ToString() != "Admin") return RedirectToAction("Dashboard", "Home");

            ViewBag.Title = "Reports";
            ViewBag.Subtitle = "View Student Ledgers and Financial Records";

            var students = new List<Dictionary<string, string>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                // mga student nga naay assessment kuhaon
                string sql = @"SELECT DISTINCT s.student_id, s.student_number, s.fname, s.lname 
                               FROM tbl_students s
                               JOIN tbl_assessments a ON s.student_id = a.student_id
                               ORDER BY s.lname";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var s = new Dictionary<string, string>();
                        s["id"] = reader["student_id"].ToString();
                        s["name"] = reader["student_number"].ToString() + " - " + reader["lname"].ToString() + ", " + reader["fname"].ToString();
                        students.Add(s);
                    }
                }
            }

            ViewBag.Students = students;
            return View();
        }

        // POST: Get Ledger Data (AJAX)
        [HttpPost]
        public JsonResult GetLedger(int studentId)
        {
            try
            {
                var ledgerRows = new List<object>();
                decimal runningBalance = 0;
                decimal totalCharges = 0;
                decimal totalPayments = 0;
                decimal totalDiscounts = 0;

                string studentInfoHtml = "";

                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // kuhaon assessment
                    string sqlAssess = @"SELECT a.*, s.student_number, s.fname, s.lname, c.course_code, s.year_level 
                                         FROM tbl_assessments a
                                         JOIN tbl_students s ON a.student_id = s.student_id
                                         JOIN tbl_courses c ON s.course_id = c.course_id
                                         WHERE a.student_id = @sid";

                    using (SqlCommand cmd = new SqlCommand(sqlAssess, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // student details
                                string name = reader["fname"].ToString() + " " + reader["lname"].ToString();
                                string details = reader["student_number"].ToString() + " · " + reader["course_code"].ToString() + " · Year " + reader["year_level"].ToString() + " · S.Y. " + reader["school_year"].ToString();
                                string initials = reader["fname"].ToString().Substring(0, 1) + reader["lname"].ToString().Substring(0, 1);

                                studentInfoHtml = $@"
                                    <div class='avatar' style='width:40px;height:40px;background:#1d4ed8;font-size:13px'>{initials.ToUpper()}</div>
                                    <div class='ledger-meta'>
                                        <div class='ledger-name'>{name}</div>
                                        <div class='ledger-info'>{details}</div>
                                    </div>";

                                // charges
                                decimal tuit = Convert.ToDecimal(reader["total_tuition"]);
                                decimal reg = Convert.ToDecimal(reader["registration_fee"]);
                                decimal misc = Convert.ToDecimal(reader["miscellaneous_fee"]);
                                decimal lab = Convert.ToDecimal(reader["laboratory_fee"]);

                                decimal scholDesc = Convert.ToDecimal(reader["scholarship_discount"]);
                                decimal otherDesc = Convert.ToDecimal(reader["other_discount"]);

                                string dateStr = Convert.ToDateTime(reader["assessed_date"]).ToString("MMM dd, yyyy");

                                // tuition
                                runningBalance += tuit; totalCharges += tuit;
                                ledgerRows.Add(new { date = dateStr, desc = "Tuition Fee", type = "Charge", charge = tuit.ToString("N2"), pay = "-", disc = "-", bal = runningBalance.ToString("N2") });

                                // reg
                                runningBalance += reg; totalCharges += reg;
                                ledgerRows.Add(new { date = dateStr, desc = "Registration Fee", type = "Charge", charge = reg.ToString("N2"), pay = "-", disc = "-", bal = runningBalance.ToString("N2") });

                                //misc
                                runningBalance += misc; totalCharges += misc;
                                ledgerRows.Add(new { date = dateStr, desc = "Miscellaneous Fee", type = "Charge", charge = misc.ToString("N2"), pay = "-", disc = "-", bal = runningBalance.ToString("N2") });

                                // add lab kung naa
                                if (lab > 0)
                                {
                                    runningBalance += lab; totalCharges += lab;
                                    ledgerRows.Add(new { date = dateStr, desc = "Laboratory Fee", type = "Charge", charge = lab.ToString("N2"), pay = "-", disc = "-", bal = runningBalance.ToString("N2") });
                                }

                                // add Discounts
                                if (scholDesc > 0)
                                {
                                    runningBalance -= scholDesc; totalDiscounts += scholDesc;
                                    ledgerRows.Add(new { date = dateStr, desc = "Scholarship Discount", type = "Discount", charge = "-", pay = "-", disc = scholDesc.ToString("N2"), bal = runningBalance.ToString("N2") });
                                }
                                if (otherDesc > 0)
                                {
                                    runningBalance -= otherDesc; totalDiscounts += otherDesc;
                                    ledgerRows.Add(new { date = dateStr, desc = "Other Discount", type = "Discount", charge = "-", pay = "-", disc = otherDesc.ToString("N2"), bal = runningBalance.ToString("N2") });
                                }
                            }
                        }
                    }

                    // get Payments
                    string sqlPay = "SELECT * FROM tbl_payments WHERE student_id = @sid ORDER BY payment_date ASC";
                    using (SqlCommand cmd = new SqlCommand(sqlPay, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal amt = Convert.ToDecimal(reader["amount_paid"]);
                                runningBalance -= amt;
                                totalPayments += amt;
                                string pDate = Convert.ToDateTime(reader["payment_date"]).ToString("MMM dd, yyyy");
                                string term = reader["payment_term"].ToString();

                                ledgerRows.Add(new { date = pDate, desc = $"{term} Payment", type = "Payment", charge = "-", pay = amt.ToString("N2"), disc = "-", bal = runningBalance.ToString("N2") });
                            }
                        }
                    }
                }

                return Json(new
                {
                    status = "ok",
                    rows = ledgerRows,
                    infoHtml = studentInfoHtml,
                    totalCharges = totalCharges.ToString("N2"),
                    totalPayments = totalPayments.ToString("N2"),
                    totalDiscounts = totalDiscounts.ToString("N2"),
                    finalBalance = runningBalance.ToString("N2")
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
        // POST: Get Assessment Report (AJAX)
        [HttpPost]
        public JsonResult GetAssessmentReport(string schoolYear)
        {
            try
            {
                var reportRows = new List<object>();
                decimal grandTotal = 0;

                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // fetch all assessments for the requested school year
                    string sql = @"
                        SELECT a.*, s.student_number, s.fname, s.lname, c.course_code 
                        FROM tbl_assessments a
                        JOIN tbl_students s ON a.student_id = s.student_id
                        JOIN tbl_courses c ON s.course_id = c.course_id
                        WHERE a.school_year = @sy
                        ORDER BY c.course_code, s.lname";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@sy", schoolYear);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal net = Convert.ToDecimal(reader["total_assessment"]);
                                grandTotal += net;

                                reportRows.Add(new
                                {
                                    studentNo = reader["student_number"].ToString(),
                                    name = reader["lname"].ToString() + ", " + reader["fname"].ToString(),
                                    course = reader["course_code"].ToString(),
                                    tuition = Convert.ToDecimal(reader["total_tuition"]).ToString("N2"),
                                    misc = Convert.ToDecimal(reader["miscellaneous_fee"]).ToString("N2"),
                                    netTotal = net.ToString("N2")
                                });
                            }
                        }
                    }
                }

                return Json(new
                {
                    status = "ok",
                    rows = reportRows,
                    grandTotal = grandTotal.ToString("N2")
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
        // GET: Report/MyLedger (STUDENT ONLY)
        public ActionResult MyLedger()
        {
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");
            if (Session["role"].ToString() != "Student") return RedirectToAction("Dashboard", "Home");

            ViewBag.Title = "My Ledger";
            ViewBag.Subtitle = "Your official statement of account";

            // Find their student_id
            int studentId = 0;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT student_id FROM tbl_users WHERE user_id = @uid", conn))
                {
                    cmd.Parameters.AddWithValue("@uid", Session["userid"].ToString());
                    studentId = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }

            // Pass it to the view so it can auto-load the ledger via AJAX
            ViewBag.MyStudentId = studentId;

            return View();
        }
    }
}