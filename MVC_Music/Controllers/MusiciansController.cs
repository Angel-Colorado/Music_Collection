using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MVC_Music.Data;
using MVC_Music.Models;
using MVC_Music.Utilities;
using MVC_Music.ViewModels;
using SkiaSharp;
using OfficeOpenXml.Style;
using OfficeOpenXml;
using System.Drawing;
using System.Reflection;
using OfficeOpenXml.Table;
using Microsoft.AspNetCore.Authorization;

namespace MVC_Music.Controllers
{
    [Authorize]
    public class MusiciansController : CustomControllers.ElephantController
    {
        private readonly MusicContext _context;

        public MusiciansController(MusicContext context)
        {
            _context = context;
        }

        // GET: Musicians
        public async Task<IActionResult> Index(string sortDirectionCheck, string sortFieldID,
            string SearchName, string SearchPhone, int? InstrumentID, int? OtherInstrumentID,
            int? page, int? pageSizeID, string actionButton, string sortDirection = "asc", string sortField = "Musician")
        {
            //Clear the sort/filter/paging URL Cookie for Controller
            CookieHelper.CookieSet(HttpContext, ControllerName() + "URL", "", -1);

            //Change colour of the button when filtering by setting this default
            ViewData["Filtering"] = "btn-outline-secondary";
            //and then in each "test" for filtering, add ViewData["Filtering"] = "btn-danger" if true;

            PopulateDropDownLists();
            ViewData["OtherInstrumentID"] = ViewData["InstrumentID"];

            //List of sort options.
            //NOTE: make sure this array has matching values to the column headings
            string[] sortOptions = new[] { "Musician", "Phone", "Age", "Primary Instrument" };

            var musicians = _context.Musicians
                .Include(p => p.MusicianThumbnail)
                .Include(m => m.Instrument)
                .Include(d => d.MusicianDocuments)
                .Include(m=>m.Plays).ThenInclude(p => p.Instrument)
                .AsNoTracking();

            //Add as many filters as needed
            if (InstrumentID.HasValue)
            {
                musicians = musicians.Where(p => p.InstrumentID == InstrumentID);
                ViewData["Filtering"] = "btn-danger";
            }
            if (OtherInstrumentID.HasValue)
            {
                musicians = musicians.Where(p => p.Plays.Any(p=>p.InstrumentID == OtherInstrumentID));
                ViewData["Filtering"] = "btn-danger";
            }
            if (!String.IsNullOrEmpty(SearchName))
            {
                musicians = musicians.Where(p => p.LastName.ToUpper().Contains(SearchName.ToUpper())
                                       || p.FirstName.ToUpper().Contains(SearchName.ToUpper()));
                ViewData["Filtering"] = "btn-danger";
            }
            if (!String.IsNullOrEmpty(SearchPhone))
            {
                musicians = musicians.Where(p => p.Phone.ToUpper().Contains(SearchPhone.ToUpper()));
                ViewData["Filtering"] = "btn-danger";
            }
            //Before we sort, see if we have called for a change of filtering or sorting
            if (!String.IsNullOrEmpty(actionButton)) //Form Submitted!
            {
                page = 1;//Reset page to start

                if (sortOptions.Contains(actionButton))//Change of sort is requested
                {
                    if (actionButton == sortField) //Reverse order on same field
                    {
                        sortDirection = sortDirection == "asc" ? "desc" : "asc";
                    }
                    sortField = actionButton;//Sort by the button clicked
                }
                else //Sort by the controls in the filter area
                {
                    sortDirection = String.IsNullOrEmpty(sortDirectionCheck) ? "asc" : "desc";
                    sortField = sortFieldID;
                }
            }

            //Now we know which field and direction to sort by
            if (sortField == "Phone")
            {
                if (sortDirection == "asc")
                {
                    musicians = musicians
                        .OrderBy(p => p.Phone)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
                else
                {
                    musicians = musicians
                        .OrderByDescending(p => p.Phone)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
            }
            else if (sortField == "Age")
            {
                if (sortDirection == "asc")
                {
                    musicians = musicians
                        .OrderByDescending(p => p.DOB)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
                else
                {
                    musicians = musicians
                        .OrderBy(p => p.DOB)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
            }
            else if (sortField == "Primary Instrument")
            {
                if (sortDirection == "asc")
                {
                    musicians = musicians
                        .OrderBy(p => p.Instrument.Name)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
                else
                {
                    musicians = musicians
                        .OrderByDescending(p => p.Instrument.Name)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
            }
            else //Sorting by Musician Name
            {
                if (sortDirection == "asc")
                {
                    musicians = musicians
                        .OrderBy(p => p.LastName.ToUpper())
                        .ThenBy(p => p.FirstName);
                }
                else
                {
                    musicians = musicians
                        .OrderByDescending(p => p.LastName.ToUpper())
                        .ThenByDescending(p => p.FirstName);
                }
            }
            //Set sort for next time
            ViewData["sortField"] = sortField;
            ViewData["sortDirection"] = sortDirection;

            //SelectList for Sorting Options
            ViewBag.sortFieldID = new SelectList(sortOptions, sortField.ToString());

            //Handle Paging
            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, "musicians");
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);
            var pagedData = await PaginatedList<Musician>.CreateAsync(musicians.AsNoTracking(), page ?? 1, pageSize);

            return View(pagedData);
        }

        // GET: Musicians/Details/5
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Musicians == null)
            {
                return NotFound();
            }

            var musician = await _context.Musicians
                .Include(p => p.MusicianPhoto)
                .Include(m => m.Instrument)
                .Include(m => m.Plays).ThenInclude(p => p.Instrument)
                .Include(m => m.Performances).ThenInclude(p => p.Song).ThenInclude(p => p.Album)
                .Include(m => m.Performances).ThenInclude(p => p.Instrument)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (musician == null)
            {
                return NotFound();
            }

            return View(musician);
        }

        // GET: Musicians/Create
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public IActionResult Create()
        {
            var musician = new Musician();
            PopulateAssignedPlaysData(musician);
            PopulateDropDownLists();
            return View();
        }

        // POST: Musicians/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public async Task<IActionResult> Create([Bind("FirstName,MiddleName,LastName,Phone,DOB," +
            "SIN,InstrumentID")] Musician musician, string[] selectedOptions, 
            IFormFile thePicture, List<IFormFile> theFiles)
        {
            try
            {
                //Add the selected plays
                if (selectedOptions != null)
                {
                    foreach (var play in selectedOptions)
                    {
                        var playToAdd = new Play { MusicianID = musician.ID, InstrumentID = int.Parse(play) };
                        musician.Plays.Add(playToAdd);
                    }
                }
                if (ModelState.IsValid)
                {
                    await AddPicture(musician, thePicture);
                    await AddDocumentsAsync(musician, theFiles);
                    _context.Add(musician);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Details", new { musician.ID });
                }
            }
            catch (RetryLimitExceededException /* dex */)
            {
                ModelState.AddModelError("", "Unable to save changes after multiple attempts. Try again, and if the problem persists, see your system administrator.");
            }
            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed"))
                {
                    ModelState.AddModelError("SIN", "Unable to save changes. Remember, you cannot have duplicate SIN numbers.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }
            PopulateAssignedPlaysData(musician);
            PopulateDropDownLists(musician);
            return View(musician);
        }

        // GET: Musicians/Edit/5
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Musicians == null)
            {
                return NotFound();
            }

            var musician = await _context.Musicians
                .Include(p => p.MusicianPhoto)
                .Include(d => d.MusicianDocuments)
                .Include(m => m.Plays).ThenInclude(p => p.Instrument)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (musician == null)
            {
                return NotFound();
            }

            if (User.IsInRole("Staff"))    // Checks for the role inside the task
            {
                if (musician.CreatedBy != User.Identity.Name)
                {
                    ModelState.AddModelError("", "As a 'Staff', you cannot edit this " +
                        "Musician because you did not enter them into the system.");
                }
            }

            PopulateAssignedPlaysData(musician);
            PopulateDropDownLists(musician);
            return View(musician);
        }

        // POST: Musicians/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public async Task<IActionResult> Edit(int id, string[] selectedOptions, 
            Byte[] RowVersion, string chkRemoveImage, IFormFile thePicture, List<IFormFile> theFiles)
        {
            var musicianToUpdate = await _context.Musicians
                .Include(p => p.MusicianPhoto)
                .Include(d => d.MusicianDocuments)
                .Include(m => m.Plays).ThenInclude(p => p.Instrument)
                .FirstOrDefaultAsync(m => m.ID == id);

            if(musicianToUpdate==null)
            {
                return NotFound();
            }

            if (User.IsInRole("Staff"))    // Checks for the role inside the task
            {
                if (musicianToUpdate.CreatedBy != User.Identity.Name)
                {
                    ModelState.AddModelError("", "As a 'Staff', you cannot edit this " +
                        "Musician because you did not enter them into the system.");

                    PopulateAssignedPlaysData(musicianToUpdate);
                    PopulateDropDownLists(musicianToUpdate);
                    return View(musicianToUpdate);
                }
            }

            //Update the plays
            UpdatePlays(selectedOptions, musicianToUpdate);

            //Put the original RowVersion value in the OriginalValues collection for the entity
            _context.Entry(musicianToUpdate).Property("RowVersion").OriginalValue = RowVersion;

            //Try updating it with the values posted
            if (await TryUpdateModelAsync<Musician>(musicianToUpdate, "",
                p => p.SIN, p => p.FirstName, p => p.MiddleName, p => p.LastName, p => p.DOB,
                p => p.Phone, p => p.InstrumentID))
            {
                try
                {
                    //For the image
                    if (chkRemoveImage != null)
                    {
                        //If we are just deleting the two versions of the photo, we need to make sure the Change Tracker knows
                        //about them both so go get the Thumbnail since we did not include it.
                        musicianToUpdate.MusicianThumbnail = _context.MusicianThumbnails.Where(p => p.MusicianID == musicianToUpdate.ID).FirstOrDefault();
                        //Then, setting them to null will cause them to be deleted from the database.
                        musicianToUpdate.MusicianPhoto = null;
                        musicianToUpdate.MusicianThumbnail = null;
                    }
                    else
                    {
                        await AddPicture(musicianToUpdate, thePicture);
                    }
                    await AddDocumentsAsync(musicianToUpdate, theFiles);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Details", new { musicianToUpdate.ID });
                }
                catch (RetryLimitExceededException /* dex */)
                {
                    ModelState.AddModelError("", "Unable to save changes after multiple attempts. Try again, and if the problem persists, see your system administrator.");
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    var exceptionEntry = ex.Entries.Single();
                    var clientValues = (Musician)exceptionEntry.Entity;
                    var databaseEntry = exceptionEntry.GetDatabaseValues();
                    if (databaseEntry == null)
                    {
                        ModelState.AddModelError("",
                            "Unable to save changes. The Musician was deleted by another user.");
                    }
                    else
                    {
                        var databaseValues = (Musician)databaseEntry.ToObject();
                        if (databaseValues.FirstName != clientValues.FirstName)
                            ModelState.AddModelError("FirstName", "Current value: "
                                + databaseValues.FirstName);
                        if (databaseValues.MiddleName != clientValues.MiddleName)
                            ModelState.AddModelError("MiddleName", "Current value: "
                                + databaseValues.MiddleName);
                        if (databaseValues.LastName != clientValues.LastName)
                            ModelState.AddModelError("LastName", "Current value: "
                                + databaseValues.LastName);
                        if (databaseValues.SIN != clientValues.SIN)
                            ModelState.AddModelError("SIN", "Current value: "
                                + databaseValues.SINFormatted);
                        if (databaseValues.DOB != clientValues.DOB)
                            ModelState.AddModelError("DOB", "Current value: "
                                + String.Format("{0:d}", databaseValues.DOB));
                        if (databaseValues.Phone != clientValues.Phone)
                            ModelState.AddModelError("Phone", "Current value: "
                                + databaseValues.PhoneFormatted);
                        //For the foreign key, we need to go to the database to get the information to show
                        if (databaseValues.InstrumentID != clientValues.InstrumentID)
                        {
                            Instrument databaseInstrument = await _context.Instruments.FirstOrDefaultAsync(i => i.ID == databaseValues.InstrumentID);
                            ModelState.AddModelError("InstrumentID", $"Current value: {databaseInstrument?.Name}");
                        }
                        ModelState.AddModelError(string.Empty, "The record you attempted to edit "
                                + "was modified by another user after you received your values. The "
                                + "edit operation was canceled and the current values in the database "
                                + "have been displayed. If you still want to save your version of this record, click "
                                + "the Save button again. Otherwise click the 'Back to Musician List' hyperlink.");
                        musicianToUpdate.RowVersion = (byte[])databaseValues.RowVersion;
                        ModelState.Remove("RowVersion");
                    }
                }
                catch (DbUpdateException dex)
                {
                    if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed"))
                    {
                        ModelState.AddModelError("SIN", "Unable to save changes. Remember, you cannot have duplicate SIN numbers.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                    }
                }
            }
            PopulateAssignedPlaysData(musicianToUpdate);
            PopulateDropDownLists(musicianToUpdate);
            return View(musicianToUpdate);
        }

        // GET: Musicians/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Musicians == null)
            {
                return NotFound();
            }

            var musician = await _context.Musicians
                .Include(m => m.Instrument)
                .Include(m => m.Plays).ThenInclude(p => p.Instrument)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            if (musician == null)
            {
                return NotFound();
            }

            return View(musician);
        }

        // POST: Musicians/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Musicians == null)
            {
                return Problem("Entity set 'MusicContext.Musicians'  is null.");
            }
            var musician = await _context.Musicians
                .Include(m => m.Instrument)
                .Include(m => m.Plays).ThenInclude(p => p.Instrument)
                .FirstOrDefaultAsync(m => m.ID == id);
            try
            {
                if (musician != null)
                {
                    _context.Musicians.Remove(musician);
                }
                await _context.SaveChangesAsync();
                return Redirect(ViewData["returnURL"].ToString());
            }
            catch (DbUpdateException)
            {
                //Note: there is really no reason a delete should fail if you can "talk" to the database.
                ModelState.AddModelError("", "Unable to delete record. Try again, and if the problem persists see your system administrator.");
            }
            return View(musician);
        }

        // The "Staff" role can't download files, only access to the Index View (MusicianDocumentsController)
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<FileContentResult> Download(int id)
        {
            var theFile = await _context.UploadedFiles
                .Include(d => d.FileContent)
                .Where(f => f.ID == id)
                .FirstOrDefaultAsync();
            return File(theFile.FileContent.Content, theFile.MimeType, theFile.FileName);
        }

        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public async Task<IActionResult> PerformanceSummary(int? page, int? pageSizeID)
        {
            var sumQ = from s in _context.PerformanceSummaries
                       orderby s.LastName.ToUpper(), s.FirstName
                       select s;

            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, "PerformanceSummary");   // Remember for this View
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);
            var pagedData = await PaginatedList<PerformanceSummaryVM>.CreateAsync(sumQ.AsNoTracking(), page ?? 1, pageSize);
            return View(pagedData);
        }
        
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public IActionResult DownloadPerformances()
        {
            //Get the Performances. References PerformanceSummaryVM (ViewModel) to have the Display Names
            var perfs = from p in _context.PerformanceSummaries
                        orderby p.LastName.ToUpper(), p.FirstName
                        select new PerformanceSummaryVM {
                            FirstName = p.FirstName,
                            MiddleName = p.MiddleName,
                            LastName = p.LastName,
                            AvgFeePaid = p.AvgFeePaid,
                            HighestFeePaid = p.HighestFeePaid,
                            LowestFeePaid = p.LowestFeePaid,
                            NumberOfPerformances = p.NumberOfPerformances,
                            NumberOfSongs = p.NumberOfSongs,
                        };

            // Selects which properties to include from the ViewModel
            string[] propertyNames = { "FormalName", "AvgFeePaid", "HighestFeePaid", "LowestFeePaid", "NumberOfPerformances", "NumberOfSongs" };
            MemberInfo[] membersToInclude = typeof(PerformanceSummaryVM)
                                            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                            .Where(p => propertyNames.Contains(p.Name))
                                            .ToArray();

            //How many rows?
            int numRows = perfs.Count();

            if (numRows > 0) //We have data
            {
                //Create a new spreadsheet from scratch.
                using (ExcelPackage excel = new ExcelPackage())
                {

                    //Note: you can also pull a spreadsheet out of the database if you
                    //have saved it in the normal way we do, as a Byte Array in a Model
                    //such as the UploadedFile class.
                    //
                    // Suppose...
                    //
                    // var theSpreadsheet = _context.UploadedFiles.Include(f => f.FileContent).Where(f => f.ID == id).SingleOrDefault();
                    //
                    //    //Pass the Byte[] FileContent to a MemoryStream
                    //
                    // using (MemoryStream memStream = new MemoryStream(theSpreadsheet.FileContent.Content))
                    // {
                    //     ExcelPackage package = new ExcelPackage(memStream);
                    // }

                    var workSheet = excel.Workbook.Worksheets.Add("Performances");

                    // Freeze the first 3 rows
                    workSheet.View.FreezePanes(4, 1);

                    //Note: Cells[row, column]
                    // Loads from the Collection, considering only the preselected columns (membersToInclude)
                    workSheet.Cells[3, 1].LoadFromCollection(perfs,
                                                             true,
                                                             TableStyles.None,
                                                             BindingFlags.Public | BindingFlags.Instance,
                                                             membersToInclude);

                    //Style fee columns for currency
                    workSheet.Column(2).Style.Numberformat.Format = "_-$* #,##0.00_-;-$* #,##0.00_-;_-$* \"0.00\"_-;_-@_-";
                    workSheet.Column(3).Style.Numberformat.Format = "_-$* #,##0.00_-;-$* #,##0.00_-;_-$* \"0.00\"_-;_-@_-";
                    workSheet.Column(4).Style.Numberformat.Format = "_-$* #,##0.00_-;-$* #,##0.00_-;_-$* \"0.00\"_-;_-@_-";

                    //Note: You can define a BLOCK of cells: Cells[startRow, startColumn, endRow, endColumn]
                    //Make Musician Bold
                    workSheet.Cells[4, 1, numRows + 3, 1].Style.Font.Bold = true;

                    using (ExcelRange avgEarnings = workSheet.Cells[numRows + 4, 1])
                    {
                        avgEarnings.Formula = "\"For \" & COUNTA(" + workSheet.Cells[4, 1].Address + ":" + workSheet.Cells[numRows + 3, 1].Address + ")& \" Musicians:\"";
                        avgEarnings.Style.Font.Bold = true;
                        avgEarnings.Style.Numberformat.Format = "###,##0";
                    }

                    using (ExcelRange avgEarnings = workSheet.Cells[numRows + 4, 2])
                    {
                        avgEarnings.Formula = "AVERAGE(" + workSheet.Cells[4, 2].Address + ":" + workSheet.Cells[numRows + 3, 2].Address + ")";
                        avgEarnings.Style.Font.Bold = true;
                        avgEarnings.Style.Numberformat.Format = "_-$* #,##0.00_-;-$* #,##0.00_-;_-$* \"0.00\"_-;_-@_-";
                    }

                    using (ExcelRange hiFee = workSheet.Cells[numRows + 4, 3])
                    {
                        hiFee.Formula = "MAX(" + workSheet.Cells[4, 3].Address + ":" + workSheet.Cells[numRows + 3, 3].Address + ")";
                        hiFee.Style.Font.Bold = true;
                        hiFee.Style.Numberformat.Format = "_-$* #,##0.00_-;-$* #,##0.00_-;_-$* \"0.00\"_-;_-@_-";
                    }

                    using (ExcelRange loFee = workSheet.Cells[numRows + 4, 4])
                    {
                        loFee.Formula = "MIN(" + workSheet.Cells[4, 4].Address + ":" + workSheet.Cells[numRows + 3, 4].Address + ")";
                        loFee.Style.Font.Bold = true;
                        loFee.Style.Numberformat.Format = "_-$* #,##0.00_-;-$* #,##0.00_-;_-$* \"0.00\"_-;_-@_-";
                    }

                    using (ExcelRange numPerfs = workSheet.Cells[numRows + 4, 5])
                    {
                        numPerfs.Formula = "SUM(" + workSheet.Cells[4, 5].Address + ":" + workSheet.Cells[numRows + 3, 5].Address + ")";
                        numPerfs.Style.Font.Bold = true;
                        numPerfs.Style.Numberformat.Format = "###,##0";
                    }

                    using (ExcelRange numSongs = workSheet.Cells[numRows + 4, 6])
                    {
                        numSongs.Formula = "SUM(" + workSheet.Cells[4, 6].Address + ":" + workSheet.Cells[numRows + 3, 6].Address + ")";
                        numSongs.Style.Font.Bold = true;
                        numSongs.Style.Numberformat.Format = "###,##0";
                    }

                    //Set Style and backgound colour of headings
                    using (ExcelRange headings = workSheet.Cells[3, 1, 3, 6])
                    {
                        headings.Style.Font.Bold = true;
                        headings.Style.Font.Color.SetColor(Color.White);
                        headings.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        var fill = headings.Style.Fill;
                        fill.PatternType = ExcelFillStyle.Solid;
                        fill.BackgroundColor.SetColor(System.Drawing.ColorTranslator.FromHtml("#0D6EFD"));
                    }

                    //Set Style and backgound colour of footer
                    using (ExcelRange footer = workSheet.Cells[numRows + 4, 1, numRows + 4, 6])
                    {
                        footer.Style.Font.Bold = true;
                        footer.Style.Font.Color.SetColor(Color.Black);
                        footer.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                        var fill = footer.Style.Fill;
                        fill.PatternType = ExcelFillStyle.Solid;
                        fill.BackgroundColor.SetColor(System.Drawing.ColorTranslator.FromHtml("#FF7501"));
                    }

                    //Autofit columns
                    workSheet.Cells.AutoFitColumns();
                    //Note: You can manually set width of columns as well
                    //workSheet.Column(7).Width = 10;

                    //Add a title and timestamp at the top of the report
                    workSheet.Cells[1, 1].Value = "Performance Summary Report";
                    using (ExcelRange Rng = workSheet.Cells[1, 1, 1, 6])
                    {
                        Rng.Merge = true;           //Merge columns start and end range
                        Rng.Style.Font.Bold = true; //Font should be bold
                        Rng.Style.Font.Size = 18;
                        Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    //Since the time zone where the server is running can be different, adjust to 
                    //Local for us.
                    DateTime utcDate = DateTime.UtcNow;
                    TimeZoneInfo esTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    DateTime localDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, esTimeZone);
                    using (ExcelRange Rng = workSheet.Cells[2, 6])
                    {
                        Rng.Value = "Created: " + localDate.ToShortTimeString() + " on " +
                            localDate.ToString("dd-MMM-yyyy");
                        Rng.Style.Font.Bold = true; //Font should be bold
                        Rng.Style.Font.Size = 12;
                        Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    //Ok, time to download the Excel
                    try
                    {
                        Byte[] theData = excel.GetAsByteArray();
                        string filename = "Performances.xlsx";
                        string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        return File(theData, mimeType, filename);
                    }
                    catch (Exception)
                    {
                        return BadRequest("Could not build and download the file.");
                    }
                }
            }
            return NotFound("No data.");
        }

        private void PopulateAssignedPlaysData(Musician musician)
        {
            //For this to work, you must have Included the Plays 
            //in the Musician
            var allOptions = _context.Instruments;
            var currentOptionIDs = new HashSet<int>(musician.Plays.Select(b => b.InstrumentID));
            var checkBoxes = new List<CheckOptionVM>();
            foreach (var option in allOptions)
            {
                checkBoxes.Add(new CheckOptionVM
                {
                    ID = option.ID,
                    DisplayText = option.Name,
                    Assigned = currentOptionIDs.Contains(option.ID)
                });
            }
            ViewData["PlayOptions"] = checkBoxes;
        }
        private void UpdatePlays(string[] selectedOptions, Musician musicianToUpdate)
        {
            if (selectedOptions == null)
            {
                musicianToUpdate.Plays = new List<Play>();
                return;
            }

            var selectedOptionsHS = new HashSet<string>(selectedOptions);
            var musicianOptionsHS = new HashSet<int>
                (musicianToUpdate.Plays.Select(c => c.InstrumentID));//IDs of the currently selected Plays
            foreach (var option in _context.Instruments)
            {
                if (selectedOptionsHS.Contains(option.ID.ToString())) //It is checked
                {
                    if (!musicianOptionsHS.Contains(option.ID))  //but not currently included
                    {
                        musicianToUpdate.Plays.Add(new Play { MusicianID = musicianToUpdate.ID, InstrumentID = option.ID });
                    }
                }
                else
                {
                    //Checkbox Not checked
                    if (musicianOptionsHS.Contains(option.ID)) //but it is currently in the history - so remove it
                    {
                        Play playToRemove = musicianToUpdate.Plays.SingleOrDefault(c => c.InstrumentID == option.ID);
                        _context.Remove(playToRemove);
                    }
                }
            }
        }

        private SelectList InstrumentList(int? selectedId)
        {
            return new SelectList(_context
                .Instruments
                .OrderBy(m => m.Name), "ID", "Name", selectedId);
        }

        private void PopulateDropDownLists(Musician musician = null)
        {
            ViewData["InstrumentID"] = InstrumentList(musician?.InstrumentID);
        }

        private async Task AddDocumentsAsync(Musician musician, List<IFormFile> theFiles)
        {
            foreach (var f in theFiles)
            {
                if (f != null)
                {
                    string mimeType = f.ContentType;
                    string fileName = Path.GetFileName(f.FileName);
                    long fileLength = f.Length;
                    //Note: you could filter for mime types if you only want to allow
                    //certain types of files.  I am allowing everything.
                    if (!(fileName == "" || fileLength == 0))//Looks like we have a file!!!
                    {
                        MusicianDocument d = new();
                        using (var memoryStream = new MemoryStream())
                        {
                            await f.CopyToAsync(memoryStream);
                            d.FileContent.Content = memoryStream.ToArray();
                        }
                        d.MimeType = mimeType;
                        d.FileName = fileName;
                        musician.MusicianDocuments.Add(d);
                    };
                }
            }
        }

        private async Task AddPicture(Musician musician, IFormFile thePicture)
        {
            //Get the picture and save it with the Musician (2 sizes)
            if (thePicture != null)
            {
                string mimeType = thePicture.ContentType;
                long fileLength = thePicture.Length;
                if (!(mimeType == "" || fileLength == 0))//Looks like we have a file!!!
                {
                    if (mimeType.Contains("image"))
                    {
                        using var memoryStream = new MemoryStream();
                        await thePicture.CopyToAsync(memoryStream);
                        var pictureArray = memoryStream.ToArray();//Gives us the Byte[]

                        //Check if we are replacing or creating new
                        if (musician.MusicianPhoto != null)
                        {
                            //We already have pictures so just replace the Byte[]
                            musician.MusicianPhoto.Content = ResizeImage.shrinkImageWebp(pictureArray, 500, 600);

                            //Get the Thumbnail so we can update it.  Remember we didn't include it
                            musician.MusicianThumbnail = _context.MusicianThumbnails.Where(p => p.MusicianID == musician.ID).FirstOrDefault();
                            musician.MusicianThumbnail.Content = ResizeImage.shrinkImageWebp(pictureArray, 75, 90);
                        }
                        else //No pictures saved so start new
                        {
                            musician.MusicianPhoto = new MusicianPhoto
                            {
                                Content = ResizeImage.shrinkImageWebp(pictureArray, 500, 600),
                                MimeType = "image/webp"
                            };
                            musician.MusicianThumbnail = new MusicianThumbnail
                            {
                                Content = ResizeImage.shrinkImageWebp(pictureArray, 75, 90),
                                MimeType = "image/webp"
                            };
                        }
                    }
                }
            }
        }

        private bool MusicianExists(int id)
        {
          return _context.Musicians.Any(e => e.ID == id);
        }
    }
}
