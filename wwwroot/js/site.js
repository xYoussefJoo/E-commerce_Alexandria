// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Delegated so it keeps working on forms that arrive via htmx-boosted page swaps,
// not just the ones present at initial DOMContentLoaded.
document.addEventListener('submit', function (e) {
    var form = e.target.closest('form.add-to-cart-form');
    if (!form) return;

    e.preventDefault();

    var submitButton = form.querySelector('button[type="submit"]');
    if (submitButton) submitButton.disabled = true;

    fetch(form.action, {
        method: 'POST',
        body: new FormData(form),
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
        credentials: 'same-origin'
    })
        .then(function (response) {
            if (response.redirected) {
                // Not authenticated - the [Authorize] filter sent us to the login page.
                window.location.href = response.url;
                return null;
            }
            return response.json();
        })
        .then(function (data) {
            if (!data) return;

            if (data.success) {
                var badge = document.getElementById('cart-badge');
                if (badge) {
                    badge.textContent = data.cartCount;
                    badge.classList.toggle('hidden', data.cartCount <= 0);
                }
                if (window.Swal) {
                    Swal.fire({
                        icon: 'success',
                        title: "'" + data.productName + "' added to your cart.",
                        toast: true,
                        position: 'top-end',
                        showConfirmButton: false,
                        timer: 2000,
                        timerProgressBar: true
                    });
                }
            } else if (window.Swal) {
                Swal.fire({
                    icon: 'error',
                    title: data.message || 'Could not add that to your cart.',
                    toast: true,
                    position: 'top-end',
                    showConfirmButton: false,
                    timer: 2500,
                    timerProgressBar: true
                });
            }
        })
        .catch(function () {
            if (window.Swal) {
                Swal.fire({ icon: 'error', title: 'Something went wrong. Please try again.', confirmButtonColor: '#6b1f2a' });
            }
        })
        .finally(function () {
            if (submitButton) submitButton.disabled = false;
        });
});

// htmx only swaps in #main-content, so <title> doesn't update on its own after a
// boosted navigation - pull it out of the full response htmx already fetched.
document.addEventListener('htmx:afterSettle', function (evt) {
    var xhr = evt.detail && evt.detail.xhr;
    if (!xhr || !xhr.responseText) return;

    var match = xhr.responseText.match(/<title>([^<]*)<\/title>/i);
    if (match) document.title = match[1];

    syncCartBadge();
});

// Boosted forms/links (cart quantity update, remove, checkout, admin edits...) go
// through a normal redirect-and-swap, so the nav badge - which lives in the header,
// outside #main-content - never gets touched by the swap itself. Resync it from the
// server after every settled navigation instead of trying to smuggle it through the swap.
function syncCartBadge() {
    fetch('/Cart/Count', { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var badge = document.getElementById('cart-badge');
            if (!badge) return;
            badge.textContent = data.count;
            badge.classList.toggle('hidden', data.count <= 0);
        })
        .catch(function () { /* non-critical, ignore */ });
}
