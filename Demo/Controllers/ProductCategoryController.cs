using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "Admin")]
public class ProductCategoryController : Controller
{
    private readonly DB db;

    public ProductCategoryController(DB db)
    {
        this.db = db;
    }

    // GET: ProductCategory/Index (redirect to Menu Maintenance)
    public IActionResult Index()
    {
        return RedirectToAction("Maintain", "Product");
    }

    // GET: ProductCategory/Create
    public IActionResult Create()
    {
        return View(new ProductCategory());
    }

    // POST: ProductCategory/Create
    [HttpPost]
    public IActionResult Create(ProductCategory m)
    {
        if (!ModelState.IsValid) return View(m);

        // Trim and uppercase id for consistency
        m.Id = (m.Id ?? "").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(m.Id))
        {
            ModelState.AddModelError("Id", "Id is required (e.g., C001).");
            return View(m);
        }

        if (db.ProductCategories.Any(c => c.Id == m.Id))
        {
            ModelState.AddModelError("Id", "Duplicate Id.");
            return View(m);
        }

        db.ProductCategories.Add(m);
        db.SaveChanges();
        TempData["Info"] = "<b>Success!</b> Category created.";
        return RedirectToAction("Maintain", "Product");
    }

    // GET: ProductCategory/Edit
    public IActionResult Edit(string id)
    {
        var m = db.ProductCategories.Find(id);
        if (m == null) return RedirectToAction("Index");
        return View(m);
    }

    // POST: ProductCategory/Edit (allow changing Id or Name)
    [HttpPost]
    public IActionResult Edit(ProductCategory m, string? oldId)
    {
        if (!ModelState.IsValid) return View(m);

        // Normalize ids
        var newId = (m.Id ?? "").Trim().ToUpperInvariant();
        var originalId = (oldId ?? m.Id ?? "").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(newId))
        {
            ModelState.AddModelError("Id", "Id is required (e.g., C001).");
            return View(m);
        }

        if (originalId == newId)
        {
            // Update name only
            var cat = db.ProductCategories.Find(originalId);
            if (cat == null) return RedirectToAction("Index");
            cat.Name = m.Name;
            db.SaveChanges();
            TempData["Info"] = "<b>Success!</b> Category updated.";
            return RedirectToAction("Maintain", "Product");
        }

        if (db.ProductCategories.Any(c => c.Id == newId))
        {
            ModelState.AddModelError("Id", "Duplicate Id.");
            return View(m);
        }

        using var tx = db.Database.BeginTransaction();

        // Create the new category with the desired Id and name
        db.ProductCategories.Add(new ProductCategory { Id = newId, Name = m.Name });
        db.SaveChanges();

        // Repoint products from original to new category id
        db.Products.Where(p => p.CategoryId == originalId)
                   .ExecuteUpdate(setters => setters.SetProperty(p => p.CategoryId, newId));

        // Remove the old category
        var old = db.ProductCategories.Find(originalId);
        if (old != null)
        {
            db.ProductCategories.Remove(old);
            db.SaveChanges();
        }

        tx.Commit();

        TempData["Info"] = "<b>Success!</b> Category Id updated and products moved.";
        return RedirectToAction("Maintain", "Product");
    }

    // POST: ProductCategory/Delete
    [HttpPost]
    public IActionResult Delete(string id)
    {
        var c = db.ProductCategories.FirstOrDefault(x => x.Id == id);
        if (c != null)
        {
            // Deleting a category will cascade delete its products (FK configured)
            db.ProductCategories.Remove(c);
            db.SaveChanges();
            TempData["Info"] = "<b>Success!</b> Category and its products deleted.";
        }
        return RedirectToAction("Maintain", "Product");
    }
}

 