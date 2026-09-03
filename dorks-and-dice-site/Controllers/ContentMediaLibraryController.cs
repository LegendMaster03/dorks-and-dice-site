@{
    ViewData["Title"] = "Admin";
    ViewData["Robots"] = "noindex,nofollow";
}

<div class="mb-4">
    <p class="text-uppercase text-muted small mb-1">Trusted administration</p>
    <h1 class="h2 mb-1">Admin</h1>
    <p class="text-body-secondary mb-0">Manage accounts and authorization. Content editing is available separately through the Editor area.</p>
</div>

<div class="row row-cols-1 row-cols-md-2 g-4">
    <div class="col">
        <a class="card h-100 text-decoration-none" href="/admin/accounts">
            <div class="card-body">
                <h2 class="h5">Account management</h2>
                <p class="text-body-secondary mb-0">Review accounts, assign global and scoped roles, lock accounts, and invalidate sessions.</p>
            </div>
        </a>
    </div>
</div>
