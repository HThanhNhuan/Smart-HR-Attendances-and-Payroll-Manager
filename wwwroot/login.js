function getRedirectByRole(role) {
    if (role === "Admin" || role === "HR" || role === "Manager") {
        return "/admin/overview.html";
    }

    if (role === "Employee") {
        return "/employee/portal.html";
    }

    return "/";
}

(function initSharedLoginPage() {
    const form = document.getElementById("sharedLoginForm");
    const errorEl = document.getElementById("loginError");

    if (!form) return;

    const savedToken = localStorage.getItem("token");
    const savedRole = localStorage.getItem("role");

    if (savedToken && savedRole) {
        window.location.href = getRedirectByRole(savedRole);
        return;
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        errorEl.textContent = "";

        const username = document.getElementById("username").value.trim();
        const password = document.getElementById("password").value;

        try {
            const res = await fetch("/api/Auth/login", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({ username, password })
            });

            const data = await res.json();

            if (!res.ok) {
                errorEl.textContent = data?.message || "Login failed.";
                return;
            }

            if (!data.role || !data.token) {
                errorEl.textContent = "Invalid login response.";
                return;
            }

            localStorage.setItem("token", data.token);
            localStorage.setItem("role", data.role);
            localStorage.setItem("username", data.username || username);
            if (data.refreshToken) localStorage.setItem("refreshToken", data.refreshToken);

            const redirectUrl = getRedirectByRole(data.role);

            if (redirectUrl === "/") {
                localStorage.removeItem("token");
                localStorage.removeItem("role");
                localStorage.removeItem("username");
                localStorage.removeItem("refreshToken");
                errorEl.textContent = "Unsupported role.";
                return;
            }

            window.location.href = redirectUrl;
        } catch (err) {
            console.error(err);
            errorEl.textContent = "Cannot connect to server.";
        }
    });
})();