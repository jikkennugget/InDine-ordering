using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

public class ProductController : Controller
{
    private readonly DB db;
    private readonly Helper hp;
    private readonly IConfiguration configuration;

    public ProductController(DB db, Helper hp, IConfiguration configuration)
    {
        this.db = db;
        this.hp = hp;
        this.configuration = configuration;
    }

    // POST: Product/AddToCart
    [HttpPost]
    [Authorize(Roles = "Staff")]
    public IActionResult AddToCart(string id, int quantity = 1)
    {
        // Validate quantity
        if (quantity <= 0 || quantity > 10)
        {
            return Json(new { success = false, error = "Invalid quantity" });
        }

        var cart = hp.GetCart();
        
        if (cart.ContainsKey(id))
        {
            cart[id] += quantity;
            // Ensure total doesn't exceed 10
            if (cart[id] > 10)
            {
                cart[id] = 10;
            }
        }
        else
        {
            cart[id] = quantity;
        }

        hp.SetCart(cart);
        return Json(new { success = true });
    }

    // POST: Product/RemoveFromCart
    [HttpPost]
    [Authorize(Roles = "Staff")]
    public IActionResult RemoveFromCart(string id, int quantity = 1)
    {
        var cart = hp.GetCart();
        
        if (cart.ContainsKey(id))
        {
            cart[id] -= quantity;
            if (cart[id] <= 0)
            {
                cart.Remove(id);
            }
        }

        hp.SetCart(cart);
        return Json(new { success = true });
    }

    // POST: Product/UpdateCart
    [HttpPost]
    [Authorize(Roles = "Staff")]
    public IActionResult UpdateCart(string productId, int quantity)
    {
        var cart = hp.GetCart();

        if (quantity >= 1 && quantity <= 10)
        {
            cart[productId] = quantity;
        }
        else
        {
            cart.Remove(productId);    
        }

        hp.SetCart(cart);

        return Redirect(Request.Headers.Referer.ToString());
    }

    // GET: Product/CartSummary (JSON)
    [HttpGet]
    [Authorize(Roles = "Staff")]
    public IActionResult CartSummary()
    {
        var cart = hp.GetCart();
        int count = cart.Sum(kv => kv.Value);

        decimal total = 0m;
        var items = new List<object>();
        
        if (cart.Count > 0)
        {
            var ids = cart.Keys.ToList();
            var prices = db.Products
                           .Where(p => ids.Contains(p.Id))
                           .Select(p => new { p.Id, p.Price })
                           .ToList();

            foreach (var kv in cart)
            {
                var price = prices.FirstOrDefault(x => x.Id == kv.Key)?.Price ?? 0m;
                total += price * kv.Value;
                items.Add(new { productId = kv.Key, quantity = kv.Value });
            }
        }

        return Json(new { count, total, items });
    }

    // GET: Product/ShoppingCart
    [Authorize(Roles = "Staff")]
    public IActionResult ShoppingCart()
    {
        var cart = hp.GetCart();
        var m = db.Products
                  .Where(p => cart.Keys.Contains(p.Id))
                  .Select(p => new CartItem
                  {
                      Product = p,
                      Quantity = cart[p.Id],
                      Subtotal = p.Price * cart[p.Id],
                  });

        if (Request.IsAjax()) return PartialView("_ShoppingCart", m);

        return View(m);
    }

    // GET: Product/Index
    public IActionResult Index(string? categoryId)
    {
        ViewBag.Cart = hp.GetCart();
        if (string.IsNullOrWhiteSpace(categoryId)) return RedirectToAction("Index", "Home");
        ViewBag.CategoryId = categoryId;
        var m = db.Products.Where(p => p.CategoryId == categoryId);
        
        if (Request.IsAjax()) return PartialView("_Index", m);

        return View(m);
    }
    
    // GET: Product/Test
    public IActionResult Test()
    {
        return View();
    }
    
    // GET: Product/TakeOrder (Staff only)
    [Authorize(Roles = "Staff")]
    public IActionResult TakeOrder(string? categoryId)
    {
        var categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            categoryId = categories.FirstOrDefault()?.Id;
        }

        ViewBag.Categories = categories;
        ViewBag.CategoryId = categoryId;

        var products = db.Products.Where(p => p.CategoryId == categoryId).OrderBy(p => p.Id);

        if (Request.IsAjax()) return PartialView("_TakeOrderGrid", products);

        return View(products);
    }

    // GET: Product/SearchProducts (Staff only)
    [Authorize(Roles = "Staff")]
    public IActionResult SearchProducts(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return PartialView("_TakeOrderGrid", new List<Product>());
        }

        // Search across all products regardless of category
        var products = db.Products
            .Include(p => p.Category)
            .Where(p => p.Name.Contains(searchTerm) || 
                       (p.Category != null && p.Category.Name.Contains(searchTerm)))
            .OrderBy(p => p.Category != null ? p.Category.Name : "")
            .ThenBy(p => p.Name)
            .ToList();

        return PartialView("_TakeOrderGrid", products);
    }

    // GET: Product/SearchOrders (Admin/Staff: search orders by table number)
    [Authorize(Roles = "Admin,Staff")]
    public IActionResult SearchOrders(string? searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                // Set ViewBag properties for empty results
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 1;
                ViewBag.TotalCount = 0;
                ViewBag.PageSize = 10;
                ViewBag.HasPreviousPage = false;
                ViewBag.HasNextPage = false;
                ViewBag.Status = null;
                return PartialView("_OrderGrid", new List<Order>());
            }

            // Search orders by table number
            var orders = db.Orders
                .Include(o => o.OrderLines)
                .ThenInclude(ol => ol.Product)
                .Where(o => o.TableNumber.Contains(searchTerm))
                .OrderByDescending(o => o.Id)
                .ToList();

            // Set ViewBag properties for search results
            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 1;
            ViewBag.TotalCount = orders.Count;
            ViewBag.PageSize = orders.Count; // Show all search results on one page
            ViewBag.HasPreviousPage = false;
            ViewBag.HasNextPage = false;
            ViewBag.Status = null;

            return PartialView("_OrderGrid", orders);
        }
        catch (Exception ex)
        {
            // Log the error and return empty results
            System.Diagnostics.Debug.WriteLine($"SearchOrders Error: {ex.Message}");
            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 1;
            ViewBag.TotalCount = 0;
            ViewBag.PageSize = 10;
            ViewBag.HasPreviousPage = false;
            ViewBag.HasNextPage = false;
            ViewBag.Status = null;
            return PartialView("_OrderGrid", new List<Order>());
        }
    }
    

    // POST: Product/CreateOrder
    [HttpPost]
    [Authorize(Roles = "Staff")]
    public IActionResult CreateOrder(string tableNumber)
    {
        var cart = hp.GetCart();
        if (cart.Count() == 0) return RedirectToAction("Order");

        if (string.IsNullOrWhiteSpace(tableNumber))
        {
            TempData["Error"] = "Table number is required.";
            return RedirectToAction("Order");
        }

        var order = new Order
        {
            Date = DateTime.Now,
            Paid = false,
            TableNumber = tableNumber.Trim(),
            PaymentMethod = "Pending",
        };
        db.Orders.Add(order);

        foreach (var (productId, quantity) in cart)
        {
            var p = db.Products.Find(productId);
            if (p == null) continue;

            order.OrderLines.Add(new()
            {
                Price = p.Price,
                Quantity = quantity,
                ProductId = productId,
            });
        }

        db.SaveChanges();
        hp.SetCart(); // Clear the cart after creating order

		TempData["Info"] = $"Order created successfully for Table {tableNumber}";
		return RedirectToAction("Order");
    }

    public IActionResult OrderComplete(int id)
    {
        ViewBag.Id = id;
        return View();
    }

	// POST: Product/CancelOrder (Staff only)
	[HttpPost]
	[Authorize(Roles = "Staff")]
	public IActionResult CancelOrder(int id)
	{
		var order = db.Orders.Include(o => o.OrderLines).FirstOrDefault(o => o.Id == id);
		if (order == null)
		{
			TempData["Error"] = "Order not found.";
			return RedirectToAction("Order");
		}

		if (order.Paid)
		{
			TempData["Error"] = "Paid orders cannot be cancelled.";
			return RedirectToAction("Order");
		}

		// Remove order and its lines
		db.OrderLines.RemoveRange(order.OrderLines);
		db.Orders.Remove(order);
		db.SaveChanges();

		TempData["Info"] = $"Order #{id} cancelled.";
		return RedirectToAction("Order");
	}

    // GET: Product/Pay
    [Authorize(Roles = "Staff")]
    public IActionResult Pay(int id)
    {
        var order = db.Orders
                      .Include(o => o.OrderLines)
                      .ThenInclude(ol => ol.Product)
                      .FirstOrDefault(o => o.Id == id);
        if (order == null) return RedirectToAction("Order");
        return View(order);
    }

    // POST: Product/Pay
    [HttpPost]
    [Authorize(Roles = "Staff")]
    public IActionResult Pay(int id, string method)
    {
        var order = db.Orders.Find(id);
        if (order == null) return RedirectToAction("Order");

        order.PaymentMethod = method;
        order.Paid = true;
        db.SaveChanges();

        TempData["Info"] = $"Payment received by {method}. Order #{id} completed.";
        return RedirectToAction("Order");
    }

    // POST: Product/MarkAsPaid
    [HttpPost]
    [Authorize(Roles = "Staff")]
    public IActionResult MarkAsPaid(int orderId)
    {
        var order = db.Orders.Find(orderId);
        if (order != null)
        {
            order.Paid = true;
            db.SaveChanges();
            TempData["Info"] = $"Order #{orderId} marked as paid.";
        }
        return RedirectToAction("Order");
    }

    // GET: Product/Order
    [Authorize(Roles = "Admin,Staff")]
    public IActionResult Order(string? status, int page = 1, int pageSize = 6)
    {
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        
        // Show orders based on status filter
        var query = db.Orders
                      .Include(o => o.OrderLines)
                      .ThenInclude(ol => ol.Product)
                      .AsQueryable();

        if (status == "paid")
        {
            query = query.Where(o => o.Paid);
        }
        else if (status == "unpaid")
        {
            query = query.Where(o => !o.Paid);
        }

        // Calculate pagination
        var totalCount = query.Count();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.HasPreviousPage = page > 1;
        ViewBag.HasNextPage = page < totalPages;

        // Calculate sales statistics
        var allOrders = db.Orders.Include(o => o.OrderLines).ToList();
        
        // Total sales (all paid orders)
        var totalSales = allOrders
            .Where(o => o.Paid)
            .Sum(o => o.OrderLines.Sum(ol => ol.Price * ol.Quantity));
        
        // Payment method counts
        var paymentMethods = allOrders
            .Where(o => o.Paid && !string.IsNullOrEmpty(o.PaymentMethod))
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();
        
        // Total paid orders count
        var totalPaidOrders = allOrders.Count(o => o.Paid);
        
        // Total unpaid orders count
        var totalUnpaidOrders = allOrders.Count(o => !o.Paid);

        ViewBag.TotalSales = totalSales;
        ViewBag.PaymentMethods = paymentMethods;
        ViewBag.TotalPaidOrders = totalPaidOrders;
        ViewBag.TotalUnpaidOrders = totalUnpaidOrders;

        var m = query.OrderByDescending(o => o.Id)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize);

        if (Request.IsAjax()) return PartialView("_OrderGrid", m);

        return View(m);
    }

    // GET: Product/Products (Admin: list all products)
    [Authorize(Roles = "Admin")]
    public IActionResult Products()
    {
        var m = db.Products
                  .Include(p => p.Category)
                  .OrderBy(p => p.CategoryId)
                  .ThenBy(p => p.Id);

        return View(m);
    }

    // GET: Product/Maintain (Admin: left categories, right product grid)
    [Authorize(Roles = "Admin")]
    public IActionResult Maintain(string? categoryId)
    {
        var categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            categoryId = categories.FirstOrDefault()?.Id;
        }

        ViewBag.Categories = categories;
        ViewBag.CategoryId = categoryId;

        var products = db.Products.Where(p => p.CategoryId == categoryId).OrderBy(p => p.Id);

        if (Request.IsAjax()) return PartialView("_ProductGrid", products);

        return View("Maintain", products);
    }

    // GET: Product/OrderDetail
    [Authorize(Roles = "Admin,Staff")]
    public IActionResult OrderDetail(int id)
    {
        var m = db.Orders
                  .Include(o => o.OrderLines)
                  .ThenInclude(ol => ol.Product)
                  .FirstOrDefault(o => o.Id == id);

        if (m == null) return RedirectToAction("Order");

        return View(m);
    }

    // POST: Product/ResetAll
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public ActionResult ResetAll()
    {
        db.Orders.ExecuteDelete();

        db.Database.ExecuteSqlRaw(@"
            DBCC CHECKIDENT (Orders, RESEED, 0);
            DBCC CHECKIDENT (OrderLines, RESEED, 0);
        ");

        return RedirectToAction("Order");
    }

    // POST: Product/ResetProducts
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult ResetProducts()
    {
        // Remove dependent order lines first
        db.OrderLines.ExecuteDelete();
        db.Products.ExecuteDelete();

        // Clear any product ids in the session cart
        hp.SetCart();

        return RedirectToAction("Index");
    }

    // GET: Product/Create
    [Authorize(Roles = "Admin")]
    public IActionResult Create(string? categoryId)
    {
		var product = new Product();
		if (!string.IsNullOrWhiteSpace(categoryId))
		{
			product.CategoryId = categoryId;
		}

		// Provide category list for dropdown in the view
		ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
		return View(product);
    }

    // POST: Product/Create
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult Create(Product m, IFormFile photoFile)
    {
        // Validate that a photo file is uploaded
        if (photoFile == null || photoFile.Length == 0)
        {
            ModelState.AddModelError("photoFile", "Please upload a product photo.");
            ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
            return View(m);
        }

        // Validate photo using Helper
        var photoError = hp.ValidatePhoto(photoFile);
        if (!string.IsNullOrEmpty(photoError))
        {
            ModelState.AddModelError("photoFile", photoError);
            ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
            return View(m);
        }

        if (!ModelState.IsValid) 
        {
            ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
            return View(m);
        }

        m.Id = (m.Id ?? "").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(m.Id) || m.Id.Length != 4)
        {
            ModelState.AddModelError("Id", "Id must be 4 characters (e.g., P001).");
			ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
			return View(m);
        }

        if (db.Products.Any(p => p.Id == m.Id))
        {
            ModelState.AddModelError("Id", "Duplicate product Id.");
			ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
			return View(m);
        }

        if (string.IsNullOrWhiteSpace(m.CategoryId))
        {
            ModelState.AddModelError("CategoryId", "Category is required. Please access this page from a category page.");
            ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
            return View(m);
        }

		// Ensure category exists to avoid FK constraint error
		m.CategoryId = m.CategoryId.Trim().ToUpperInvariant();
        if (!db.ProductCategories.Any(c => c.Id == m.CategoryId))
		{
			ModelState.AddModelError("CategoryId", "Selected category does not exist. Create the category first.");
            ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
            return View(m);
		}

        // Handle photo upload using product ID and category as filename
        var fileNameWithCategory = $"{m.CategoryId}_{m.Id}";
        System.Diagnostics.Debug.WriteLine($"ProductController: Creating product with filename: {fileNameWithCategory}");
        var photoFileName = hp.SavePhoto(photoFile, "products", fileNameWithCategory);
        System.Diagnostics.Debug.WriteLine($"ProductController: Photo saved as: {photoFileName}");
        m.PhotoURL = photoFileName;

        db.Products.Add(m);
        db.SaveChanges();
        TempData["Info"] = "<b>Success!</b> Product created.";
        return RedirectToAction("Maintain", new { categoryId = m.CategoryId });
    }

    // GET: Product/Edit
    [Authorize(Roles = "Admin")]
    public IActionResult Edit(string id)
    {
        var m = db.Products.Find(id);
        if (m == null) return RedirectToAction("Index");
        ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
        return View(m);
    }

    // POST: Product/Edit
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult Edit(Product m, IFormFile? photoFile)
    {
        if (!ModelState.IsValid) 
        {
            ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
            return View(m);
        }

        var e = db.Products.Find(m.Id);
        if (e == null) return RedirectToAction("Index");

		// Validate category exists before saving
		var newCategoryId = (m.CategoryId ?? "").Trim().ToUpperInvariant();
		
		if (string.IsNullOrWhiteSpace(newCategoryId))
		{
			ModelState.AddModelError("CategoryId", "Please select a category.");
			ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
			return View(m);
		}
		
		if (!db.ProductCategories.Any(c => c.Id == newCategoryId))
		{
			ModelState.AddModelError("CategoryId", "Selected category does not exist. Please select a valid category.");
			ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
			return View(m);
		}

		e.Name = m.Name;
		e.Price = m.Price;
		e.CategoryId = newCategoryId;

        // Handle photo upload (optional for edit)
        if (photoFile != null && photoFile.Length > 0)
        {
            // Validate photo using Helper
            var photoError = hp.ValidatePhoto(photoFile);
            if (!string.IsNullOrEmpty(photoError))
            {
                ModelState.AddModelError("photoFile", photoError);
                ViewBag.Categories = db.ProductCategories.OrderBy(c => c.Id).ToList();
                return View(m);
            }

            // Delete old photo and save new one using product ID and category as filename
            hp.DeletePhoto(e.PhotoURL, "products");
            var fileNameWithCategory = $"{e.CategoryId}_{e.Id}";
            var photoFileName = hp.SavePhoto(photoFile, "products", fileNameWithCategory);
            e.PhotoURL = photoFileName;
        }

        db.SaveChanges();

        TempData["Info"] = "<b>Success!</b> Product updated.";
        return RedirectToAction("Maintain", new { categoryId = e.CategoryId });
    }

    // POST: Product/Delete
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult Delete(string id)
    {
        var p = db.Products.Find(id);
        var categoryId = p?.CategoryId;
        if (p != null)
        {
            // Delete associated photo files
            hp.DeletePhoto(p.PhotoURL, "products");
            
            db.Products.Remove(p);
            db.SaveChanges();
            TempData["Info"] = "<b>Success!</b> Product deleted.";
        }
        return RedirectToAction("Maintain", new { categoryId });
    }
}
