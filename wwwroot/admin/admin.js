
const AdminApp = (() => {
  const state = {
    token: localStorage.getItem("token"),
    role: localStorage.getItem("role"),
    username: localStorage.getItem("username"),
    currentPage: document.body.dataset.page || "overview",
    employees: [],
    attendances: [],
    attendanceAdjustments: [],
    payrolls: [],
    leaves: [],
    departments: [],
    positions: [],
    leaveApprovers: [],
    charts: {},
    paging: {
      employees: { page: 1, pageSize: 8 },
      attendances: { page: 1, pageSize: 10 },
      payrolls: { page: 1, pageSize: 10 },
      leaves: { page: 1, pageSize: 10 },
      departments: { page: 1, pageSize: 10 },
      attendanceAdjustments: { page: 1, pageSize: 8 }
    }
  };

  const endpoints = {
    overview: (y,m) => `/api/Dashboard/overview?year=${y}&month=${m}`,
    attendanceMonthly: (y,m) => `/api/Dashboard/attendance-monthly?year=${y}&month=${m}`,
    payrollMonthly: (y,m) => `/api/Dashboard/payroll-monthly?year=${y}&month=${m}`,
    leaveMonthly: (y,m) => `/api/Dashboard/leave-monthly?year=${y}&month=${m}`,
    deptHeadcount: `/api/Dashboard/department-headcount`,
    recentLeave: take => `/api/Dashboard/recent-leave-requests?take=${take}`,
    recentAttendance: take => `/api/Dashboard/recent-attendances?take=${take}`,
    recentPayroll: take => `/api/Dashboard/recent-payrolls?take=${take}`,
    employeeStatus: `/api/Dashboard/employee-status-summary`,
    employees: `/api/Employees`,
    attendances: params => `/api/Attendances${params ? `?${params}` : ""}`,
    payrolls: params => `/api/Payrolls${params ? `?${params}` : ""}`,
    departments: `/api/Departments`,
    positions: `/api/Positions`,
    leaves: `/api/LeaveRequests`,
    leavesPending: `/api/LeaveRequests/pending`
  };

  const roleProfiles = {
    Admin: {
      workspace: "Admin command center",
      shellLabel: "Smart HR / Admin command center",
      sidebarSubtitle: "Admin Console",
      allowedPages: ["overview", "employees", "attendances", "payrolls", "leaves", "departments", "reports", "schedules", "overtime"],
      hiddenHrefs: [],
      heroHint: "Full configuration, workforce structure, and deletion controls are available.",
      accentClass: "role-admin"
    },
    HR: {
      workspace: "HR operations workspace",
      shellLabel: "Smart HR / HR operations workspace",
      sidebarSubtitle: "HR Operations",
      allowedPages: ["overview", "employees", "attendances", "payrolls", "leaves", "reports", "schedules", "overtime"],
      hiddenHrefs: ["/admin/departments.html"],
      heroHint: "Operational approval, attendance, payroll, and employee workflows are enabled without system-structure controls.",
      accentClass: "role-hr"
    },
    Manager: {
      workspace: "Manager review workspace",
      shellLabel: "Smart HR / Manager review workspace",
      sidebarSubtitle: "Manager Operations",
      allowedPages: ["overview", "attendances", "leaves", "reports", "schedules", "overtime"],
      hiddenHrefs: ["/admin/employees.html", "/admin/payrolls.html", "/admin/departments.html"],
      heroHint: "Managers can review operational queues, schedules, and overtime without admin-level structure controls.",
      accentClass: "role-hr"
    }
  };

  function q(id) { return document.getElementById(id); }
  function esc(v) {
    return String(v ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }
  function money(v) {
    return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 }).format(v || 0);
  }
  function dt(v) {
    if (!v) return "";
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return esc(v);
    return d.toLocaleDateString("vi-VN");
  }
  function dtTime(v) {
    if (!v) return "";
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return esc(v);
    return `${d.toLocaleDateString("vi-VN")} ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
  }
  function statusBadgeClass(status) {
    const v = String(status || "").toLowerCase();
    if (v === "approved") return "badge-approved";
    if (v === "pending") return "badge-pending";
    if (v === "rejected") return "badge-rejected";
    if (v === "cancelled") return "badge-cancelled";
    if (v === "present") return "badge-present";
    if (v === "late") return "badge-late";
    if (v === "absent") return "badge-absent";
    if (v === "leave") return "badge-leave";
    if (v === "remote") return "badge-remote";
    if (v === "active") return "badge-active";
    return "badge-inactive";
  }
  function showError(msg) {
    const host = q("pageErrorHost");
    if (!host) return;
    host.innerHTML = `<div class="alert alert-danger" role="alert">${esc(msg)}</div>`;
  }
  function clearError() {
    const host = q("pageErrorHost");
    if (host) host.innerHTML = "";
  }

  async function ensureDepartmentsLoaded() {
    if (Array.isArray(state.departments) && state.departments.length) return state.departments;
    const [departments, headcount] = await Promise.all([
      api(endpoints.departments),
      api(endpoints.deptHeadcount).catch(() => [])
    ]);
    const headMap = new Map((headcount || []).map(x => [String(x.departmentCode || x.departmentId), x]));
    state.departments = (Array.isArray(departments) ? departments : []).map(d => {
      const matched = headMap.get(String(d.departmentCode)) || {};
      return {
        ...d,
        employeeCount: matched.employeeCount ?? d.employeeCount ?? 0,
        activeEmployeeCount: matched.activeEmployeeCount ?? d.activeEmployeeCount ?? 0,
        inactiveEmployeeCount: matched.inactiveEmployeeCount ?? d.inactiveEmployeeCount ?? 0
      };
    });
    return state.departments;
  }

  function syncDepartmentFilterOptions() {
    const select = q("adjDepartmentId");
    if (!select) return;
    const current = select.value;
    const options = ['<option value="">All departments</option>'].concat(
      (state.departments || []).map(x => `<option value="${x.id}">${esc(x.departmentCode || '')} - ${esc(x.departmentName || '')}</option>`)
    );
    select.innerHTML = options.join('');
    if ([...select.options].some(opt => opt.value === current)) select.value = current;
  }


  async function ensureAiTemplatesLoaded() {
    if (Array.isArray(state.aiTemplates) && state.aiTemplates.length) return state.aiTemplates;
    const templates = await api('/api/Ai/payroll-summary/templates').catch(() => []);
    state.aiTemplates = Array.isArray(templates) ? templates : [];
    return state.aiTemplates;
  }

  function populateDepartmentSelect(selectId, includeAllLabel = 'All departments') {
    const select = q(selectId);
    if (!select) return;
    const current = select.value;
    const baseOptions = [`<option value="">${esc(includeAllLabel)}</option>`].concat((state.departments || []).map(dep => `<option value="${dep.id}">${esc(dep.departmentCode || '')} - ${esc(dep.departmentName || '')}</option>`));
    select.innerHTML = baseOptions.join('');
    if ([...select.options].some(opt => opt.value === current)) select.value = current;
    if (state.role === 'Manager') {
      select.value = '';
      select.disabled = true;
    }
  }

    function populateAiTemplateControls(selectId, helperId, promptId, defaultTemplateKey = 'anomaly-overview') {
        const select = q(selectId);
        if (!select) return;

        const helper = helperId ? q(helperId) : null;
        const prompt = promptId ? q(promptId) : null;
        const templates = Array.isArray(state.aiTemplates) ? state.aiTemplates : [];

        const getTemplateKey = (t) => String(t?.templateKey || t?.key || '');
        const getTemplateTitle = (t) => String(t?.title || t?.name || 'Untitled template');

        select.innerHTML = templates.length
            ? templates.map(t => `<option value="${esc(getTemplateKey(t))}">${esc(getTemplateTitle(t))}</option>`).join('')
            : '<option value="anomaly-overview">Anomaly overview</option>';

        if ([...select.options].some(opt => opt.value === defaultTemplateKey)) {
            select.value = defaultTemplateKey;
        }

        const syncTemplate = () => {
            const item = templates.find(t => getTemplateKey(t) === String(select.value));

            if (helper) {
                helper.innerHTML = item
                    ? `<strong>${esc(item.title || item.name || '')}</strong>
           <span>${esc(item.description || '')}</span>
           <small>Template: ${esc(item.promptTemplate || item.templateKey || item.key || '')}</small>`
                    : 'No prompt template metadata is available.';
            }

            if (prompt) {
                const existing = prompt.value.trim();
                if (!existing || prompt.dataset.autoFilled === 'true') {
                    prompt.value = item?.examplePrompt || '';
                    prompt.dataset.autoFilled = 'true';
                }
            }
        };

        select.onchange = () => {
            if (prompt) prompt.dataset.autoFilled = 'true';
            syncTemplate();
        };

        if (prompt) {
            prompt.oninput = () => {
                prompt.dataset.autoFilled = 'false';
            };
        }

        syncTemplate();
    }

  function renderAiSummaryResult(hostId, response, scopeLabel = 'Payroll summary assistant') {
    const host = q(hostId);
    if (!host) return;
    if (!response) {
      host.innerHTML = '<div class="empty-state">No AI summary has been generated yet.</div>';
      return;
    }
    const highlights = Array.isArray(response.highlights) ? response.highlights : [];
    host.innerHTML = `
      <div class="ai-summary-card ${response.fromCache ? 'from-cache' : 'fresh'}">
        <div class="ai-summary-top">
          <div>
            <span class="ai-summary-chip">${esc(scopeLabel)}</span>
            <h4>${esc(response.templateTitle || 'Payroll Summary Assistant')}</h4>
          </div>
          <div class="ai-summary-meta">
            <span class="status-pill ${response.fromCache ? 'status-approved' : 'status-pending'}">${response.fromCache ? 'Cache hit' : 'Fresh run'}</span>
            <small>${esc(dtTime(response.generatedAt))}</small>
          </div>
        </div>
        <p class="ai-summary-body">${esc(response.summary || 'No summary available.')}</p>
        <div class="ai-summary-prompt">
          <strong>Prompt used</strong>
          <span>${esc(response.promptUsed || '-')}</span>
        </div>
        <div class="ai-summary-highlights">
          ${highlights.length ? highlights.map(item => `<div class="ai-highlight-item"><i class="bi bi-stars"></i><span>${esc(item)}</span></div>`).join('') : '<div class="empty-state">No highlight bullets were generated.</div>'}
        </div>
      </div>`;
  }

  async function runAiPayrollSummary({ month, year, departmentId, templateKey, prompt, messageId, resultId, scopeLabel }) {
    const message = messageId ? q(messageId) : null;
    if (message) {
      message.className = 'small text-muted';
      message.textContent = 'Generating payroll summary...';
    }
    try {
      const response = await api('/api/Ai/payroll-summary', 'POST', {
        month: Number(month),
        year: Number(year),
        departmentId: departmentId ? Number(departmentId) : null,
        templateKey: templateKey || 'anomaly-overview',
        prompt: prompt || null
      });
      renderAiSummaryResult(resultId, response, scopeLabel);
      if (message) {
        message.className = `small ${response.fromCache ? 'text-success' : 'text-primary'}`;
        message.textContent = response.fromCache ? 'Assistant response loaded from cache.' : 'Assistant response generated successfully.';
      }
      return response;
    } catch (err) {
      if (message) {
        message.className = 'small text-danger';
        message.textContent = err.message || 'Failed to generate payroll summary.';
      }
      throw err;
    }
  }

  async function api(url, method = "GET", body = null) {
    const headers = {};
    if (state.token) headers["Authorization"] = `Bearer ${state.token}`;
    if (body != null) headers["Content-Type"] = "application/json";

    const res = await fetch(url, {
      method,
      headers,
      body: body != null ? JSON.stringify(body) : undefined
    });

    if (res.status === 401 || res.status === 403) {
      logout();
      throw new Error("Unauthorized");
    }

    const contentType = res.headers.get("content-type") || "";
    const data = contentType.includes("json")
      ? await res.json().catch(() => null)
      : await res.text().catch(() => "");

    if (!res.ok) {
      const message =
        typeof data === "string" && data
          ? data
          : data?.message || data?.title || `Request failed (${res.status})`;
      throw new Error(message);
    }
    return data;
  }

  async function logout() {
    try {
      const refreshToken = localStorage.getItem("refreshToken");
      await fetch("/api/Auth/logout", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Authorization": `Bearer ${state.token}` },
        body: JSON.stringify({ refreshToken })
      });
    } catch {}
    localStorage.removeItem("token");
    localStorage.removeItem("role");
    localStorage.removeItem("username");
    localStorage.removeItem("refreshToken");
    window.location.href = "/login.html";
  }

  function guard() {
    if (!state.token || !state.role || !["Admin", "HR", "Manager"].includes(state.role)) {
      window.location.href = "/login.html";
      return false;
    }
    return true;
  }

  function ensureAdminNavExtensions() {
    const nav = document.querySelector('.sidebar-nav');
    if (!nav) return;
    const links = [
      { href: '/admin/schedules.html', nav: 'schedules', icon: 'bi bi-calendar3-week-fill', label: 'Schedules' },
      { href: '/admin/overtime.html', nav: 'overtime', icon: 'bi bi-clock-history', label: 'Overtime' }
    ];
    links.forEach(item => {
      if (nav.querySelector(`[href="${item.href}"]`)) return;
      const link = document.createElement('a');
      link.href = item.href;
      link.className = 'sidebar-link';
      link.dataset.nav = item.nav;
      link.innerHTML = `<i class="${item.icon}"></i><span>${item.label}</span>`;
      const reports = nav.querySelector('[data-nav="reports"]');
      nav.insertBefore(link, reports || null);
    });
  }

  function currentRoleProfile() {
    return roleProfiles[state.role] || roleProfiles.Admin;
  }

  function isAdmin() {
    return state.role === "Admin";
  }

  function isHr() {
    return state.role === "HR";
  }

  function buildQuery(params) {
    const query = new URLSearchParams();
    Object.entries(params || {}).forEach(([key, value]) => {
      if (value === undefined || value === null || value === "") return;
      query.set(key, value);
    });
    return query.toString();
  }

  async function downloadFileFromApi(url, fallbackName) {
    const headers = {};
    if (state.token) headers["Authorization"] = `Bearer ${state.token}`;
    const res = await fetch(url, { headers });

    if (res.status === 401 || res.status === 403) {
      logout();
      throw new Error("Unauthorized");
    }

    if (!res.ok) {
      const message = await res.text().catch(() => "Download failed.");
      throw new Error(message || "Download failed.");
    }

    const blob = await res.blob();
    const disposition = res.headers.get("content-disposition") || "";
    const matched = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition);
    const rawName = matched?.[1] || matched?.[2] || fallbackName;
    const fileName = decodeURIComponent(rawName);

    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
  }

  function fmtInputDate(value) {
    if (!value) return "";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "";
    return d.toISOString().slice(0, 10);
  }

  function fmtInputDateTime(value) {
    if (!value) return "";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "";
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, "0");
    const dd = String(d.getDate()).padStart(2, "0");
    const hh = String(d.getHours()).padStart(2, "0");
    const mi = String(d.getMinutes()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd}T${hh}:${mi}`;
  }

  function fmtInputTime(value) {
    if (!value) return "";
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "";
    return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
  }

  function combineLocalDateTime(dateValue, timeValue) {
    if (!dateValue || !timeValue) return null;
    return `${dateValue}T${timeValue}`;
  }

  function renderAuditTrail(request) {
    const reviewState = request.reviewedAt
      ? `<div class="audit-item"><strong>Reviewed</strong><span>${esc(request.reviewedByUsername || "-" )} · ${dtTime(request.reviewedAt)}</span><small>${esc(request.reviewNote || "No review note.")}</small></div>`
      : `<div class="audit-item"><strong>Awaiting review</strong><span>Pending reviewer assignment</span><small>The request is still in the approval queue.</small></div>`;

    return `
      <div class="audit-trail-card">
        <div class="audit-item">
          <strong>Submitted</strong>
          <span>${dtTime(request.createdAt)}</span>
          <small>${esc(request.fullName || "Employee")} requested <strong>${esc(request.requestedStatus || "-")}</strong> for ${dt(request.workDate)}.</small>
        </div>
        ${reviewState}
        <div class="audit-item">
          <strong>Requested correction</strong>
          <span>${request.requestedCheckInTime ? dtTime(request.requestedCheckInTime) : "No check-in"} → ${request.requestedCheckOutTime ? dtTime(request.requestedCheckOutTime) : "No check-out"}</span>
          <small>${esc(request.reason || "No reason provided.")}</small>
        </div>
      </div>`;
  }

  function renderActivityItems(hostId, items, emptyText) {
    const host = q(hostId);
    if (!host) return;
    host.innerHTML = items && items.length
      ? items.map(item => `
        <div class="audit-item">
          <strong>${esc(item.title)}</strong>
          <span>${esc(item.subtitle || "-")}</span>
          <small>${esc(item.meta || "-")}</small>
          ${item.code ? `<code>${esc(item.code)}</code>` : ""}
        </div>`).join("")
      : `<div class="empty-state">${esc(emptyText)}</div>`;
  }

  async function fetchLeaveHistory(id) {
    try {
      const items = await api(`/api/LeaveRequests/${id}/history`);
      return Array.isArray(items) ? items : [];
    } catch {
      return [];
    }
  }

  async function fetchPayrollHistory(id) {
    try {
      const items = await api(`/api/Payrolls/${id}/history`);
      return Array.isArray(items) ? items : [];
    } catch {
      return [];
    }
  }

  async function fetchAdjustmentHistory(id) {
    try {
      const items = await api(`/api/Attendances/adjustment-requests/${id}/history`);
      return Array.isArray(items) ? items : [];
    } catch {
      return [];
    }
  }

  function enforceRolePageAccess() {
    ensureAdminNavExtensions();
    const profile = currentRoleProfile();
    if (!profile.allowedPages.includes(state.currentPage)) {
      window.location.href = state.role === "Employee" ? "/employee/overview.html" : "/admin/overview.html";
      return false;
    }
    return true;
  }

  function initShell() {
    const profile = currentRoleProfile();
    document.body.classList.add(profile.accentClass);
    q("adminCurrentUser") && (q("adminCurrentUser").textContent = `${state.username || "User"} (${state.role || ""})`);
    q("adminRoleChip") && (q("adminRoleChip").textContent = profile.shellLabel);
    const sidebarSubtitle = document.querySelector(".sidebar-brand-copy p");
    if (sidebarSubtitle) sidebarSubtitle.textContent = profile.sidebarSubtitle;
    document.querySelectorAll(".sidebar-link").forEach(a => {
      if (profile.hiddenHrefs.includes((a.getAttribute("href") || "").toLowerCase())) {
        a.style.display = "none";
      }
    });
    const roleBanner = document.createElement("div");
    roleBanner.className = "role-experience-banner";
    roleBanner.innerHTML = `<strong>${esc(profile.workspace)}</strong><span>${esc(profile.heroHint)}</span>`;
    const heroPanel = document.querySelector(".hero-panel");
    if (heroPanel && !document.querySelector(".role-experience-banner")) {
      heroPanel.insertAdjacentElement("beforebegin", roleBanner);
    }

    const path = location.pathname.toLowerCase();
    document.querySelectorAll(".sidebar-link").forEach(a => {
      a.classList.toggle("active", a.getAttribute("href").toLowerCase() === path);
    });

    q("adminLogoutBtn")?.addEventListener("click", logout);

    const shell = document.querySelector(".admin-shell");
    const mainToggle = q("adminMainToggle");
    const sidebarToggle = q("adminSidebarToggle");
    const backdrop = q("adminSidebarBackdrop");
    const savedCollapsed = localStorage.getItem("admin_sidebar_collapsed") === "true";

    function isMobile() { return window.innerWidth <= 991.98; }
    function applyDesktopState() {
      if (!shell) return;
      if (isMobile()) {
        shell.classList.remove("sidebar-collapsed");
        return;
      }
      shell.classList.toggle("sidebar-collapsed", savedCollapsed);
    }
    function toggleSidebar() {
      if (!shell) return;
      if (isMobile()) {
        shell.classList.toggle("sidebar-open");
      } else {
        shell.classList.toggle("sidebar-collapsed");
        localStorage.setItem("admin_sidebar_collapsed", shell.classList.contains("sidebar-collapsed") ? "true" : "false");
      }
    }
    mainToggle?.addEventListener("click", toggleSidebar);
    sidebarToggle?.addEventListener("click", toggleSidebar);
    backdrop?.addEventListener("click", () => shell?.classList.remove("sidebar-open"));
    window.addEventListener("resize", () => {
      if (!shell) return;
      if (isMobile()) {
        shell.classList.remove("sidebar-collapsed");
      } else {
        shell.classList.remove("sidebar-open");
        const collapsed = localStorage.getItem("admin_sidebar_collapsed") === "true";
        shell.classList.toggle("sidebar-collapsed", collapsed);
      }
    });
    applyDesktopState();
  }

  function renderPaging(containerId, key, itemsLength, onRender) {
    const cfg = state.paging[key];
    const totalPages = Math.max(1, Math.ceil(itemsLength / cfg.pageSize));
    cfg.page = Math.min(cfg.page, totalPages);
    const host = q(containerId);
    if (!host) return;
    host.innerHTML = `
      <div class="page-bar">
        <div class="small-meta">Showing page <strong>${cfg.page}</strong> of <strong>${totalPages}</strong> · ${itemsLength} records</div>
        <div class="page-controls">
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page <= 1 ? "disabled" : ""} data-page-action="first">First</button>
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page <= 1 ? "disabled" : ""} data-page-action="prev">Prev</button>
          <span class="page-indicator">${cfg.page}/${totalPages}</span>
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page >= totalPages ? "disabled" : ""} data-page-action="next">Next</button>
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page >= totalPages ? "disabled" : ""} data-page-action="last">Last</button>
        </div>
      </div>`;
    host.querySelectorAll("[data-page-action]").forEach(btn => {
      btn.addEventListener("click", () => {
        const act = btn.dataset.pageAction;
        if (act === "first") cfg.page = 1;
        if (act === "prev") cfg.page = Math.max(1, cfg.page - 1);
        if (act === "next") cfg.page = Math.min(totalPages, cfg.page + 1);
        if (act === "last") cfg.page = totalPages;
        onRender();
      });
    });
  }

  function slicePaged(items, key) {
    const cfg = state.paging[key];
    const start = (cfg.page - 1) * cfg.pageSize;
    return items.slice(start, start + cfg.pageSize);
  }

  function setStatGrid(id, pairs) {
    const host = q(id);
    if (!host) return;
    host.innerHTML = pairs.map(([label, value]) => `
      <div class="stat-box">
        <span>${esc(label)}</span>
        <strong>${esc(value)}</strong>
      </div>
    `).join("");
  }


  async function initOverview() {
    const y = q("yearInput");
    const m = q("monthInput");
    const refresh = q("refreshBtn");
    const now = new Date();
    if (y) y.value = now.getFullYear();
    if (m) m.value = now.getMonth() + 1;

    function setRoleKpiGrid(hostId, items) {
      const host = q(hostId);
      if (!host) return;
      host.innerHTML = (items || []).map(item => `
        <div class="role-kpi-card">
          <div class="mini-badge"><i class="bi ${esc(item.icon || 'bi-speedometer2')}"></i><span>${esc(item.badge || 'KPI')}</span></div>
          <span>${esc(item.label)}</span>
          <strong>${esc(item.value)}</strong>
          <small>${esc(item.note || '-')}</small>
        </div>`).join("");
    }

    function renderRoleFocusChart(labels, values, colors) {
      const el = q("roleFocusChart");
      if (!el || typeof Chart === "undefined") return;
      state.charts.roleFocus?.destroy?.();
      state.charts.roleFocus = new Chart(el, {
        type: "bar",
        data: {
          labels,
          datasets: [{
            label: "Role focus",
            data: values,
            backgroundColor: colors,
            borderRadius: 12,
            maxBarThickness: 44
          }]
        },
        options: {
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: {
            y: { beginAtZero: true, grid: { color: "rgba(148,163,184,.16)" } },
            x: { grid: { display: false } }
          }
        }
      });

    }

    function renderDepartmentComparison(items) {
      const body = q('repDepartmentBody');
      const rows = Array.isArray(items) ? [...items].sort((a, b) => Number(b.totalNetSalary || 0) - Number(a.totalNetSalary || 0)) : [];
      if (body) {
        body.innerHTML = rows.length ? rows.map((item, index) => `
          <tr>
            <td>
              <div class="d-flex flex-column gap-1">
                <span class="department-rank-chip"><i class="bi bi-diagram-3"></i> #${index + 1}</span>
                <strong>${esc(item.departmentName || '-')}</strong>
                <small class="text-muted">${esc(item.departmentCode || '-')}</small>
              </div>
            </td>
            <td>${item.headcount ?? 0}</td>
            <td>${item.activeEmployees ?? 0}</td>
            <td>${item.attendanceRecords ?? 0}</td>
            <td>${item.approvedLeaveCount ?? 0}</td>
            <td>${(item.pendingLeaveCount ?? 0) + (item.pendingAdjustmentCount ?? 0)}</td>
            <td>${item.payrollCoverage ?? 0}</td>
            <td>${money(item.totalNetSalary)}</td>
          </tr>`).join('') : `<tr><td colspan="8"><div class="empty-state">No department comparison data is available for the selected month.</div></td></tr>`;
      }
      renderReportChart('reportsDepartmentComparison', 'repDepartmentComparisonChart', {
        type: 'bar',
        data: {
          labels: rows.map(item => item.departmentName || '-'),
          datasets: [
            { label: 'Headcount', data: rows.map(item => item.headcount ?? 0), backgroundColor: 'rgba(37,99,235,.82)', borderRadius: 10, maxBarThickness: 34 },
            { label: 'Pending actions', data: rows.map(item => (item.pendingLeaveCount ?? 0) + (item.pendingAdjustmentCount ?? 0)), backgroundColor: 'rgba(245,158,11,.82)', borderRadius: 10, maxBarThickness: 34 },
            { label: 'Payroll coverage', data: rows.map(item => item.payrollCoverage ?? 0), backgroundColor: 'rgba(16,185,129,.82)', borderRadius: 10, maxBarThickness: 34 }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { position: 'bottom' } },
          scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
          onClick: (_, elements, chart) => {
            if (!elements.length) return;
            const label = chart.data.labels?.[elements[0].index];
            if (!label) return;
            reportState.drill.scope = 'employee';
            reportState.drill.status = '';
            reportState.drill.search = label;
            reportState.drill.context = `Department comparison selected: ${label}. The drill-down table now searches employees and records related to that department.`;
            if (drillScope) drillScope.value = 'employee';
            if (drillSearch) drillSearch.value = label;
            setDrillStatusOptions('employee');
            renderDrillDown();
          }
        }
      });
    }

    function renderTrendCharts(items) {
      const rows = Array.isArray(items) ? items : [];
      const metric = reportState.trendMetric || 'totalNetSalary';
      const metricLabelMap = {
        totalNetSalary: 'Total Net Salary',
        attendanceRecords: 'Attendance Records',
        approvedLeaves: 'Approved Leaves',
        pendingAdjustments: 'Pending Adjustments',
        averageNetSalary: 'Average Net Salary'
      };
      renderReportChart('reportsTrend', 'repTrendChart', {
        type: metric === 'totalNetSalary' || metric === 'averageNetSalary' ? 'line' : 'bar',
        data: {
          labels: rows.map(item => item.periodLabel || '-'),
          datasets: [{
            label: metricLabelMap[metric] || 'Trend',
            data: rows.map(item => Number(item[metric] || 0)),
            backgroundColor: 'rgba(37,99,235,.24)',
            borderColor: 'rgba(37,99,235,.95)',
            fill: metric === 'totalNetSalary' || metric === 'averageNetSalary',
            tension: 0.32,
            borderWidth: 3,
            pointRadius: 4,
            pointHoverRadius: 5,
            borderRadius: 10,
            maxBarThickness: 40
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: { y: { beginAtZero: true } },
          onClick: async (_, elements) => {
            if (!elements.length) return;
            const selected = rows[elements[0].index];
            if (!selected) return;
            if (year) year.value = selected.year;
            if (month) month.value = selected.month;
            reportState.drill.scope = metric === 'approvedLeaves' ? 'leave' : metric === 'pendingAdjustments' ? 'adjustment' : metric === 'attendanceRecords' ? 'attendance' : 'payroll';
            reportState.drill.status = metric === 'approvedLeaves' ? 'Approved' : metric === 'pendingAdjustments' ? 'Pending' : '';
            reportState.drill.search = '';
            reportState.drill.context = `Monthly trend selected: ${selected.periodLabel}. The report workspace has been refreshed to that month.`;
            if (drillScope) drillScope.value = reportState.drill.scope;
            if (drillSearch) drillSearch.value = '';
            setDrillStatusOptions(reportState.drill.scope);
            if (drillStatus) drillStatus.value = reportState.drill.status;
            load();
          }
        }
      });
    }


    async function refreshPeriodPanel() {
      if (!periodStatus || !year?.value) return;
      try {
        const periods = await api(`/api/Payrolls/periods?year=${year.value}`);
        const current = Array.isArray(periods) ? periods.find(x => Number(x.payrollYear) === Number(year.value) && Number(x.payrollMonth) === Number(month?.value || 0)) : null;
        const locked = current?.isLocked === true;
        periodStatus.textContent = locked ? 'Locked' : 'Open';
        periodStatus.className = `status-pill ${locked ? 'status-rejected' : 'status-approved'}`;
        periodMeta.textContent = current ? `${locked ? 'Locked' : 'Open'} period ${String(current.payrollMonth).padStart(2,'0')}/${current.payrollYear}${current.lockedByUsername ? ` · ${current.lockedByUsername}` : ''}${current.lockedAt ? ` · ${dtTime(current.lockedAt)}` : ''}` : `No explicit period record yet for ${String(month?.value || '').padStart(2,'0')}/${year.value}.`;
        if (periodToggleBtn) periodToggleBtn.textContent = locked ? 'Unlock period' : 'Lock period';
        if (periodToggleBtn) periodToggleBtn.dataset.locked = locked ? 'true' : 'false';
      } catch (err) {
          const is404 = String(err?.message || '').includes('404');
          if (periodStatus) {
              periodStatus.textContent = is404 ? 'Not configured' : 'Unavailable';
              periodStatus.className = 'status-pill status-pending';
          }
          if (periodMeta) {
              periodMeta.textContent = is404
                  ? 'Payroll period API is not configured yet in the backend.'
                  : (err.message || 'Could not load payroll period status.');
          }
          if (periodToggleBtn) {
              periodToggleBtn.disabled = is404;
              if (is404) periodToggleBtn.textContent = 'Period API missing';
          }
      }
    }

    async function togglePeriodLock() {
      const currentLocked = periodToggleBtn?.dataset.locked === 'true';
      const note = prompt(currentLocked ? 'Optional unlock note' : 'Optional lock note', '') ?? '';
      try {
        await api(`/api/Payrolls/periods/${year?.value}/${month?.value}/lock`, 'PUT', { isLocked: !currentLocked, note });
        await refreshPeriodPanel();
        await load();
      } catch (err) {
        showError(err.message || 'Failed to update payroll period lock.');
      }
    }
    async function load() {
      try {
        clearError();
        const year = y?.value || now.getFullYear();
        const month = m?.value || (now.getMonth() + 1);
        const [overview, att, pay, leave, headcount, recentLeave, recentAttendance, recentPayroll, empStatus, pendingAdjustments, allAdjustments, leaveAudit, payrollAudit] = await Promise.all([
          api(endpoints.overview(year, month)),
          api(endpoints.attendanceMonthly(year, month)),
          api(endpoints.payrollMonthly(year, month)),
          api(endpoints.leaveMonthly(year, month)),
          api(endpoints.deptHeadcount),
          api(endpoints.recentLeave(5)),
          api(endpoints.recentAttendance(5)),
          api(endpoints.recentPayroll(5)),
          api(endpoints.employeeStatus),
          api(`/api/Attendances/adjustment-requests?${buildQuery({ status: "Pending", month, year })}`).catch(() => []),
          api(`/api/Attendances/adjustment-requests?${buildQuery({ month, year })}`).catch(() => []),
          api('/api/LeaveRequests/history/recent?take=6').catch(() => []),
          api('/api/Payrolls/history/recent?take=6').catch(() => [])
        ]);

        const adjustments = Array.isArray(allAdjustments) ? allAdjustments : [];
        const adjustmentPendingCount = adjustments.filter(x => String(x.status || '').toLowerCase() === 'pending').length;
        const adjustmentReviewedCount = adjustments.filter(x => String(x.status || '').toLowerCase() !== 'pending').length;
        const approvalBacklog = (leave.pendingRequests ?? 0) + adjustmentPendingCount;
        const payrollCoverage = overview.activeEmployees ? `${pay.totalPayrollRecords ?? 0}/${overview.activeEmployees}` : `${pay.totalPayrollRecords ?? 0}/0`;

        q("metricEmployees").textContent = overview.totalEmployees ?? 0;
        q("metricActive").textContent = overview.activeEmployees ?? 0;
        q("metricPendingLeaves").textContent = overview.pendingLeaveRequests ?? 0;
        q("metricNetSalary").textContent = money(overview.monthlyTotalNetSalary);

        setStatGrid("attendanceSummaryGrid", [
          ["Present", att.presentCount ?? 0], ["Late", att.lateCount ?? 0], ["Absent", att.absentCount ?? 0],
          ["Leave", att.leaveCount ?? 0], ["Remote", att.remoteCount ?? 0], ["Total", att.totalAttendanceRecords ?? 0]
        ]);
        setStatGrid("payrollSummaryGrid", [
          ["Total Payrolls", pay.totalPayrollRecords ?? 0], ["Total Bonus", money(pay.totalBonus)], ["Total Deduction", money(pay.totalDeduction)],
          ["Total Net Salary", money(pay.totalNetSalary)], ["Average Net Salary", money(pay.averageNetSalary)]
        ]);
        setStatGrid("leaveSummaryGrid", [
          ["Total Requests", leave.totalRequests ?? 0], ["Pending", leave.pendingRequests ?? 0], ["Approved", leave.approvedRequests ?? 0],
          ["Rejected", leave.rejectedRequests ?? 0], ["Cancelled", leave.cancelledRequests ?? 0], ["Approved Days", leave.approvedLeaveDays ?? 0]
        ]);
        setStatGrid("employeeStatusGrid", [
          ["Total", empStatus.totalEmployees ?? 0], ["Active", empStatus.activeEmployees ?? 0], ["Inactive", empStatus.inactiveEmployees ?? 0],
          ["Active %", `${empStatus.activePercentage ?? 0}%`], ["Inactive %", `${empStatus.inactivePercentage ?? 0}%`]
        ]);

        q("departmentBody").innerHTML = (headcount || []).map(x => `
          <tr><td>${esc(x.departmentCode)}</td><td>${esc(x.departmentName)}</td><td>${x.employeeCount ?? 0}</td><td>${x.activeEmployeeCount ?? 0}</td><td>${x.inactiveEmployeeCount ?? 0}</td></tr>
        `).join("") || `<tr><td colspan="5"><div class="empty-state">No department data.</div></td></tr>`;

        q("recentLeaveBody").innerHTML = (recentLeave || []).map(x => `
          <tr><td>${esc(x.fullName)}</td><td>${esc(x.leaveType)}</td><td><span class="badge-soft ${statusBadgeClass(x.status)}">${esc(x.status)}</span></td><td>${dt(x.startDate)} - ${dt(x.endDate)}</td></tr>
        `).join("") || `<tr><td colspan="4"><div class="empty-state">No leave activity.</div></td></tr>`;

        q("recentAttendanceBody").innerHTML = (recentAttendance || []).map(x => `
          <tr><td>${esc(x.fullName)}</td><td>${dt(x.workDate)}</td><td><span class="badge-soft ${statusBadgeClass(x.status)}">${esc(x.status)}</span></td><td>${esc(x.note || "-")}</td></tr>
        `).join("") || `<tr><td colspan="4"><div class="empty-state">No attendance activity.</div></td></tr>`;

        q("recentPayrollBody").innerHTML = (recentPayroll || []).map(x => `
          <tr><td>${esc(x.fullName)}</td><td>${esc(`${x.payrollMonth}/${x.payrollYear}`)}</td><td>${money(x.netSalary)}</td><td>${dtTime(x.generatedAt)}</td></tr>
        `).join("") || `<tr><td colspan="4"><div class="empty-state">No payroll activity.</div></td></tr>`;

        const overviewTitle = q("overviewHeroTitle");
        const overviewText = q("overviewHeroText");
        if (isAdmin()) {
          if (overviewTitle) overviewTitle.textContent = "Admin Governance Dashboard";
          if (overviewText) overviewText.textContent = "Admin sees organization-wide controls, workforce structure health, approval queues, and audit-sensitive payroll activity in one governance-oriented dashboard.";
          setStatGrid("rolePrimaryStats", [["Departments", overview.totalDepartments ?? 0], ["Positions", overview.totalPositions ?? 0], ["Pending Adjustments", adjustmentPendingCount], ["Payroll Records", pay.totalPayrollRecords ?? 0]]);
          q("rolePrimaryTitle") && (q("rolePrimaryTitle").textContent = "Admin control tower");
          q("rolePrimarySubtitle") && (q("rolePrimarySubtitle").textContent = "Governance-oriented indicators for the current month.");
          q("rolePrimaryNote") && (q("rolePrimaryNote").innerHTML = `<strong>Admin focus:</strong> Track structure health, approval backlog, payroll volume, and audit-heavy changes before exporting or locking reports.`);
          const host = q("adminGovernanceList");
          if (host) host.innerHTML = [
            `${overview.totalDepartments ?? 0} departments and ${overview.totalPositions ?? 0} positions are currently configured in the organization structure.`,
            `${empStatus.inactiveEmployees ?? 0} employees are inactive and may require data cleanup or access review.`,
            `${adjustmentPendingCount} attendance adjustments and ${leave.pendingRequests ?? 0} leave requests are waiting for action.`,
            `${leaveAudit.length || 0} leave audit events and ${payrollAudit.length || 0} payroll audit events are visible in the recent governance feed.`
          ].map(item => `<div class="inline-metric-item"><i class="bi bi-shield-check"></i><span>${esc(item)}</span></div>`).join("");

          setRoleKpiGrid("adminDeepKpiGrid", [
            { badge: "Structure", icon: "bi-diagram-3", label: "Departments / Positions", value: `${overview.totalDepartments ?? 0} / ${overview.totalPositions ?? 0}`, note: "Configured organization structure for the workspace." },
            { badge: "Approvals", icon: "bi-hourglass-split", label: "Approval backlog", value: approvalBacklog, note: "Pending leave decisions plus attendance adjustments waiting for action." },
            { badge: "Audit", icon: "bi-journal-code", label: "Recent audit volume", value: (leaveAudit.length || 0) + (payrollAudit.length || 0), note: "Recent leave and payroll audit events captured on the dashboard." },
            { badge: "Payroll", icon: "bi-cash-coin", label: "Payroll coverage", value: payrollCoverage, note: "Generated payroll records against active employees for the selected month." },
            { badge: "Exposure", icon: "bi-person-dash", label: "Inactive exposure", value: `${empStatus.inactivePercentage ?? 0}%`, note: "Inactive workforce ratio that may require governance review." },
            { badge: "Compensation", icon: "bi-graph-up-arrow", label: "Average net salary", value: money(pay.averageNetSalary), note: "Average monthly net salary across generated payroll records." }
          ]);

          q("roleFocusChartTitle") && (q("roleFocusChartTitle").textContent = "Admin governance profile");
          q("roleFocusChartSubtitle") && (q("roleFocusChartSubtitle").textContent = "Structure, approval pressure, inactivity, and audit activity for the current snapshot.");
          q("roleFocusHighlightsTitle") && (q("roleFocusHighlightsTitle").textContent = "Admin talking points");
          q("roleFocusHighlightsSubtitle") && (q("roleFocusHighlightsSubtitle").textContent = "Use these bullets when explaining governance-level observations.");
          renderRoleFocusChart(
            ["Departments", "Positions", "Inactive", "Open approvals", "Audit events"],
            [overview.totalDepartments ?? 0, overview.totalPositions ?? 0, empStatus.inactiveEmployees ?? 0, approvalBacklog, (leaveAudit.length || 0) + (payrollAudit.length || 0)],
            ["rgba(43,107,255,.82)", "rgba(16,185,129,.82)", "rgba(245,158,11,.82)", "rgba(239,68,68,.82)", "rgba(124,58,237,.82)"]
          );
          const roleHighlights = q("roleFocusHighlights");
          if (roleHighlights) roleHighlights.innerHTML = [
            `Governance snapshot: ${approvalBacklog} total approval items are still open across leave and attendance-correction workflows.`,
            `Payroll coverage currently stands at ${payrollCoverage}, so admin can quickly spot whether active employees are fully covered for the month.`,
            `${empStatus.inactiveEmployees ?? 0} inactive employees represent ${empStatus.inactivePercentage ?? 0}% of the workforce and may need access or data cleanup review.`,
            `Recent audit visibility combines ${leaveAudit.length || 0} leave events and ${payrollAudit.length || 0} payroll events for compliance-oriented demonstrations.`
          ].map(item => `<div class="inline-metric-item"><i class="bi bi-broadcast-pin"></i><span>${esc(item)}</span></div>`).join("");
        } else {
          if (overviewTitle) overviewTitle.textContent = "HR Operations Dashboard";
          if (overviewText) overviewText.textContent = "HR sees approval workload, employee-service requests, and monthly operations in an action-first dashboard designed for review and follow-up.";
          setStatGrid("rolePrimaryStats", [["Pending Leaves", leave.pendingRequests ?? 0], ["Pending Adjustments", adjustmentPendingCount], ["Present", att.presentCount ?? 0], ["Monthly Payrolls", pay.totalPayrollRecords ?? 0]]);
          q("rolePrimaryTitle") && (q("rolePrimaryTitle").textContent = "HR action desk");
          q("rolePrimarySubtitle") && (q("rolePrimarySubtitle").textContent = "Operational review indicators for the current month.");
          q("rolePrimaryNote") && (q("rolePrimaryNote").innerHTML = `<strong>HR focus:</strong> Clear approvals quickly, monitor attendance corrections, and validate payroll operations before month-end reporting.`);
          const host = q("hrOperationsList");
          if (host) host.innerHTML = [
            `${leave.pendingRequests ?? 0} leave requests are waiting for decision in the selected month.`,
            `${adjustmentPendingCount} attendance adjustments still need HR/Admin review.`,
            `${recentLeave.filter(x => String(x.status || '').toLowerCase() === 'pending').length} recent leave submissions were still pending in the latest activity feed.`,
            `${recentPayroll.length || 0} recent payroll events are visible to support follow-up with employees.`
          ].map(item => `<div class="inline-metric-item"><i class="bi bi-clipboard2-pulse"></i><span>${esc(item)}</span></div>`).join("");

          setRoleKpiGrid("hrDeepKpiGrid", [
            { badge: "Queue", icon: "bi-journal-check", label: "Pending leave requests", value: leave.pendingRequests ?? 0, note: "Leave items still waiting for HR/Admin decision." },
            { badge: "Corrections", icon: "bi-arrow-repeat", label: "Pending adjustments", value: adjustmentPendingCount, note: "Attendance corrections still in the approval pipeline." },
            { badge: "Throughput", icon: "bi-check2-square", label: "Reviewed adjustments", value: adjustmentReviewedCount, note: "Attendance-adjustment requests that already completed the review workflow." },
            { badge: "Attendance", icon: "bi-person-check", label: "Monthly present records", value: att.presentCount ?? 0, note: "Present statuses captured in the selected month." },
            { badge: "Risk", icon: "bi-exclamation-triangle", label: "Late attendance", value: att.lateCount ?? 0, note: "Late records that may need HR follow-up or coaching." },
            { badge: "Payroll", icon: "bi-wallet2", label: "Payroll ready coverage", value: payrollCoverage, note: "Generated payroll coverage versus active employees for month-end operations." }
          ]);

          q("roleFocusChartTitle") && (q("roleFocusChartTitle").textContent = "HR operations profile");
          q("roleFocusChartSubtitle") && (q("roleFocusChartSubtitle").textContent = "Approval workload, service throughput, and attendance quality indicators for HR." );
          q("roleFocusHighlightsTitle") && (q("roleFocusHighlightsTitle").textContent = "HR talking points");
          q("roleFocusHighlightsSubtitle") && (q("roleFocusHighlightsSubtitle").textContent = "Use these bullets when explaining operational workload and service quality.");
          renderRoleFocusChart(
            ["Pending leave", "Pending adj.", "Reviewed adj.", "Late", "Payroll records"],
            [leave.pendingRequests ?? 0, adjustmentPendingCount, adjustmentReviewedCount, att.lateCount ?? 0, pay.totalPayrollRecords ?? 0],
            ["rgba(245,158,11,.82)", "rgba(239,68,68,.82)", "rgba(16,185,129,.82)", "rgba(124,58,237,.82)", "rgba(43,107,255,.82)"]
          );
          const roleHighlights = q("roleFocusHighlights");
          if (roleHighlights) roleHighlights.innerHTML = [
            `HR backlog currently contains ${leave.pendingRequests ?? 0} leave requests and ${adjustmentPendingCount} attendance adjustments that still need action.`,
            `${adjustmentReviewedCount} adjustment requests have already moved through the review stage, which helps demonstrate operational throughput.`,
            `${att.lateCount ?? 0} late attendance records are visible for the selected month, providing an early signal for workforce support or escalation.`,
            `Payroll preparation has covered ${pay.totalPayrollRecords ?? 0} employees so far, while average net salary remains ${money(pay.averageNetSalary)} for the snapshot.`
          ].map(item => `<div class="inline-metric-item"><i class="bi bi-clipboard-data"></i><span>${esc(item)}</span></div>`).join("");
        }

        const queueRows = [
          ...(Array.isArray(pendingAdjustments) ? pendingAdjustments.slice(0, 5).map(x => ({ type: 'Attendance adjustment', owner: x.fullName, detail: `${x.requestedStatus} · ${dt(x.workDate)}`, status: x.status, created: dtTime(x.createdAt) })) : []),
          ...(Array.isArray(recentLeave) ? recentLeave.filter(x => String(x.status || '').toLowerCase() === 'pending').slice(0, 5).map(x => ({ type: 'Leave request', owner: x.fullName, detail: `${x.leaveType} · ${dt(x.startDate)} → ${dt(x.endDate)}`, status: x.status, created: dtTime(x.createdAt) })) : [])
        ].slice(0, 8);
        const queueBody = q('overviewQueueBody');
        if (queueBody) {
          queueBody.innerHTML = queueRows.length ? queueRows.map(row => `
            <tr>
              <td>${esc(row.type)}</td>
              <td>${esc(row.owner || '-')}</td>
              <td>${esc(row.detail || '-')}</td>
              <td><span class="badge-soft ${statusBadgeClass(row.status)}">${esc(row.status)}</span></td>
              <td>${esc(row.created || '-')}</td>
            </tr>`).join('') : `<tr><td colspan="5"><div class="empty-state">No pending approval items for the selected month.</div></td></tr>`;
        }

        const auditItems = [
          ...(Array.isArray(leaveAudit) ? leaveAudit.slice(0, 3).map(x => ({ title: `Leave ${x.actionType}`, subtitle: `${x.performedByUsername || 'System'} · ${dtTime(x.createdAt)}`, meta: `${x.previousStatus || '-'} → ${x.newStatus || '-'}${x.note ? ` · ${x.note}` : ''}` })) : []),
          ...(Array.isArray(payrollAudit) ? payrollAudit.slice(0, 3).map(x => ({ title: `Payroll ${x.actionType}`, subtitle: `${x.employeeFullName || '-'} · ${dtTime(x.createdAt)}`, meta: `${x.payrollMonth}/${x.payrollYear} · Net ${money(x.netSalary)}${x.note ? ` · ${x.note}` : ''}` })) : [])
        ];
        renderActivityItems('overviewAuditFeed', auditItems.slice(0, 6), 'No recent audit feed is available yet.');

        renderAttendanceChart(att);
        renderLeaveChart(leave);
      } catch (err) {
        showError(err.message || "Failed to load overview.");
      }
    }

    refresh?.addEventListener("click", load);
    load();
  }

  function renderAttendanceChart(data) {
    const el = q("attendanceChart");
    if (!el || typeof Chart === "undefined") return;
    state.charts.attendance?.destroy();
    state.charts.attendance = new Chart(el, {
      type: "bar",
      data: {
        labels: ["Present", "Late", "Absent", "Leave", "Remote"],
        datasets: [{
          label: "Attendance Count",
          data: [data.presentCount||0, data.lateCount||0, data.absentCount||0, data.leaveCount||0, data.remoteCount||0],
          backgroundColor: ["#2b6bff", "#ff9800", "#ef4444", "#8b5cf6", "#10b981"],
          borderRadius: 14, borderSkipped: false
        }]
      },
      options: {
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          y: { beginAtZero: true, ticks: { stepSize: 1 }, grid: { color: "rgba(148,163,184,.16)" } },
          x: { grid: { display: false } }
        }
      }
    });
  }

  function renderLeaveChart(data) {
    const el = q("leaveChart");
    if (!el || typeof Chart === "undefined") return;
    state.charts.leave?.destroy();
    state.charts.leave = new Chart(el, {
      type: "doughnut",
      data: {
        labels: ["Pending", "Approved", "Rejected", "Cancelled"],
        datasets: [{
          data: [data.pendingRequests||0, data.approvedRequests||0, data.rejectedRequests||0, data.cancelledRequests||0],
          backgroundColor: ["#ff9800", "#2b6bff", "#ef4444", "#64748b"],
          borderWidth: 0, hoverOffset: 8
        }]
      },
      options: {
        maintainAspectRatio: false, cutout: "62%",
        plugins: { legend: { position: "bottom", labels: { usePointStyle: true, boxWidth: 10, padding: 18 } } }
      }
    });
  }

  async function initEmployees() {
    const search = q("employeesSearch");
    const dept = q("employeesDepartment");
    const status = q("employeesStatus");
    const pageSize = q("employeesPageSize");
    const refresh = q("employeesRefresh");
    const createBtn = q("employeesCreateBtn");

    async function load() {
      try {
        clearError();
        const [employees, departments, positions] = await Promise.all([
          api(endpoints.employees),
          api(endpoints.departments),
          api(endpoints.positions)
        ]);
        state.employees = Array.isArray(employees) ? employees : [];
        state.departments = Array.isArray(departments) ? departments : [];
        state.positions = Array.isArray(positions) ? positions : [];
        dept.innerHTML = `<option value="">All departments</option>` + state.departments.map(d => `<option value="${d.id}">${esc(d.departmentName)}</option>`).join("");
        renderEmployees();
      } catch (err) {
        showError(err.message || "Failed to load employees.");
      }
    }

    function employeeFormHtml(item = null) {
      const hasAccount = item?.hasLoginAccount ? "checked" : "";
      const isActive = item?.isActive === false ? "" : "checked";
      const deptOptions = state.departments.map(d => `<option value="${d.id}" ${String(item?.departmentId ?? "") === String(d.id) ? "selected" : ""}>${esc(d.departmentName)}</option>`).join("");
      const posOptions = state.positions.map(p => `<option value="${p.id}" ${String(item?.positionId ?? "") === String(p.id) ? "selected" : ""}>${esc(p.positionName)}</option>`).join("");
      const accountRole = item?.accountRole || "Employee";
      return `
        <form id="employeeUpsertForm" class="row g-3">
          <div class="col-md-6"><label class="form-label">Employee Code</label><input class="form-control" id="empCodeInput" value="${esc(item?.employeeCode || "")}" required /></div>
          <div class="col-md-6"><label class="form-label">Full Name</label><input class="form-control" id="empFullNameInput" value="${esc(item?.fullName || "")}" required /></div>
          <div class="col-md-6"><label class="form-label">Email</label><input type="email" class="form-control" id="empEmailInput" value="${esc(item?.email || "")}" required /></div>
          <div class="col-md-6"><label class="form-label">Base Salary</label><input type="number" step="0.01" class="form-control" id="empSalaryInput" value="${item?.baseSalary ?? ""}" required /></div>
          <div class="col-md-6"><label class="form-label">Hire Date</label><input type="date" class="form-control" id="empHireDateInput" value="${item?.hireDate ? new Date(item.hireDate).toISOString().slice(0,10) : ""}" required /></div>
          <div class="col-md-6 d-flex align-items-end"><div class="form-check form-switch"><input class="form-check-input" type="checkbox" id="empActiveInput" ${isActive}><label class="form-check-label" for="empActiveInput">Active employee</label></div></div>
          <div class="col-md-6"><label class="form-label">Department</label><select class="form-select" id="empDepartmentInput" required><option value="">Select department</option>${deptOptions}</select></div>
          <div class="col-md-6"><label class="form-label">Position</label><select class="form-select" id="empPositionInput" required><option value="">Select position</option>${posOptions}</select></div>
          <div class="col-12"><div class="form-check form-switch"><input class="form-check-input" type="checkbox" id="empHasAccountInput" ${hasAccount}><label class="form-check-label" for="empHasAccountInput">Create or keep linked login account</label></div></div>
          <div class="col-md-6 account-field"><label class="form-label">Username</label><input class="form-control" id="empUsernameInput" value="${esc(item?.username || "")}" placeholder="Username" /></div>
          <div class="col-md-6 account-field"><label class="form-label">Role</label><select class="form-select" id="empAccountRoleInput"><option value="Employee" ${accountRole === "Employee" ? "selected" : ""}>Employee</option>${isAdmin() ? `<option value="HR" ${accountRole === "HR" ? "selected" : ""}>HR</option><option value="Manager" ${accountRole === "Manager" ? "selected" : ""}>Manager</option>` : ``}</select><div class="form-help">${isAdmin() ? "Admin can provision HR and Manager accounts." : "HR can only provision employee logins."}</div></div>
          <div class="col-12 account-field"><label class="form-label">${item ? "Reset password (optional)" : "Initial password"}</label><input type="password" class="form-control" id="empPasswordInput" placeholder="${item ? "Leave blank to keep current password" : "Create first password"}" /></div>
          <div class="col-12"><div id="employeeFormMessage" class="small text-danger"></div></div>
        </form>`;
    }

    function bindAccountToggle() {
      const hasAccountInput = q("empHasAccountInput");
      const toggle = () => {
        const hidden = !hasAccountInput?.checked;
        document.querySelectorAll(".account-field").forEach(el => {
          el.style.display = hidden ? "none" : "";
        });
      };
      hasAccountInput?.addEventListener("change", toggle);
      toggle();
    }

    async function openEmployeeModal(item = null) {
      const submitBtn = await openFormModal({
        title: item ? "Edit Employee" : "Create Employee",
        bodyHtml: employeeFormHtml(item),
        submitText: item ? "Save changes" : "Create employee"
      });
      bindAccountToggle();
      submitBtn?.addEventListener("click", async () => {
        const message = q("employeeFormMessage");
        message.textContent = "";
        const hasAccount = q("empHasAccountInput")?.checked;
        const payload = {
          employeeCode: q("empCodeInput")?.value?.trim(),
          fullName: q("empFullNameInput")?.value?.trim(),
          email: q("empEmailInput")?.value?.trim(),
          baseSalary: Number(q("empSalaryInput")?.value || 0),
          hireDate: q("empHireDateInput")?.value,
          isActive: !!q("empActiveInput")?.checked,
          departmentId: Number(q("empDepartmentInput")?.value || 0),
          positionId: Number(q("empPositionInput")?.value || 0),
          createLoginAccount: hasAccount,
          hasLoginAccount: hasAccount,
          username: q("empUsernameInput")?.value?.trim() || null,
          password: q("empPasswordInput")?.value || null,
          newPassword: q("empPasswordInput")?.value || null,
          accountRole: q("empAccountRoleInput")?.value || null
        };
        try {
          if (item) {
            await api(`/api/Employees/${item.id}`, "PUT", payload);
          } else {
            await api(endpoints.employees, "POST", payload);
          }
          closeSharedModal();
          await load();
        } catch (err) {
          message.textContent = err.message || "Failed to save employee.";
        }
      }, { once: true });
    }

    async function removeEmployee(id, fullName) {
      if (!confirm(`Delete employee ${fullName}? Linked login account will also be removed.`)) return;
      try {
        await api(`/api/Employees/${id}`, "DELETE");
        await load();
      } catch (err) {
        showError(err.message || "Failed to delete employee.");
      }
    }

    function renderEmployees() {
      const term = (search?.value || "").trim().toLowerCase();
      const deptId = dept?.value || "";
      const activeState = status?.value || "";
      state.paging.employees.pageSize = Number(pageSize?.value || 8);

      let items = [...state.employees];
      if (term) {
        items = items.filter(x =>
          [x.employeeCode, x.fullName, x.email, x.departmentName, x.positionName, x.username, x.accountRole]
            .filter(Boolean)
            .join(" ")
            .toLowerCase()
            .includes(term)
        );
      }
      if (deptId) items = items.filter(x => String(x.departmentId) === String(deptId));
      if (activeState) items = items.filter(x => String(x.isActive) === activeState);

      const pageItems = slicePaged(items, "employees");
      const body = q("employeesBody");
      body.innerHTML = pageItems.length
        ? pageItems.map(x => `
          <tr>
            <td><strong>${esc(x.employeeCode)}</strong></td>
            <td>${esc(x.fullName)}</td>
            <td>${esc(x.email || "-")}</td>
            <td>${esc(x.departmentName || "-")}</td>
            <td>${esc(x.positionName || "-")}</td>
            <td>${money(x.baseSalary)}</td>
            <td>${dt(x.hireDate)}</td>
            <td><span class="badge-soft ${x.isActive ? "badge-active" : "badge-inactive"}">${x.isActive ? "Active" : "Inactive"}</span></td>
            <td>${x.hasLoginAccount ? `<div><strong>${esc(x.username || "-")}</strong><br><small class="text-muted">${esc(x.accountRole || "-")}</small></div>` : `<span class="text-muted">No account</span>`}</td>
            <td>
              <div class="action-group">
                <button class="btn btn-sm btn-outline-primary" data-view-id="${x.id}">View</button>
                <button class="btn btn-sm btn-outline-secondary" data-edit-id="${x.id}">Edit</button>
                ${state.role === "Admin" ? `<button class="btn btn-sm btn-outline-danger" data-delete-id="${x.id}">Delete</button>` : ""}
              </div>
            </td>
          </tr>
        `).join("")
        : `<tr><td colspan="10"><div class="empty-state">No employees match the current filters.</div></td></tr>`;

      renderPaging("employeesPaging", "employees", items.length, renderEmployees);
      setStatGrid("employeeQuickStats", [
        ["Employees", items.length],
        ["Active", items.filter(x => x.isActive).length],
        ["With account", items.filter(x => x.hasLoginAccount).length],
        ["HR accounts", items.filter(x => String(x.accountRole) === "HR").length]
      ]);

      body.querySelectorAll("[data-view-id]").forEach(btn => {
        btn.addEventListener("click", async () => {
          try {
            const id = btn.dataset.viewId;
            const emp = await api(`/api/Employees/${id}`);
            await openDetailModal("Employee Details", `
              <div class="row g-3">
                <div class="col-md-6"><label class="form-label">Employee Code</label><input class="form-control" value="${esc(emp.employeeCode)}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Full Name</label><input class="form-control" value="${esc(emp.fullName)}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Email</label><input class="form-control" value="${esc(emp.email || "")}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Status</label><input class="form-control" value="${emp.isActive ? "Active" : "Inactive"}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Department</label><input class="form-control" value="${esc(emp.departmentName || "")}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Position</label><input class="form-control" value="${esc(emp.positionName || "")}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Base Salary</label><input class="form-control" value="${money(emp.baseSalary)}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Hire Date</label><input class="form-control" value="${dt(emp.hireDate)}" readonly /></div>
                <div class="col-md-6"><label class="form-label">Login Account</label><input class="form-control" value="${emp.hasLoginAccount ? `${emp.username} (${emp.accountRole})` : "No linked account"}" readonly /></div>
              </div>
            `, false);
          } catch (err) {
            showError(err.message || "Failed to load employee details.");
          }
        });
      });

      body.querySelectorAll("[data-edit-id]").forEach(btn => {
        btn.addEventListener("click", async () => {
          const item = state.employees.find(x => String(x.id) === String(btn.dataset.editId));
          if (item) await openEmployeeModal(item);
        });
      });

      body.querySelectorAll("[data-delete-id]").forEach(btn => {
        btn.addEventListener("click", () => {
          const item = state.employees.find(x => String(x.id) === String(btn.dataset.deleteId));
          if (item) removeEmployee(item.id, item.fullName);
        });
      });
    }

    [search, dept, status, pageSize].forEach(el => el?.addEventListener("input", () => { state.paging.employees.page = 1; renderEmployees(); }));
    [dept, status, pageSize].forEach(el => el?.addEventListener("change", () => { state.paging.employees.page = 1; renderEmployees(); }));
    refresh?.addEventListener("click", load);
    createBtn?.addEventListener("click", () => openEmployeeModal());
    load();
  }

  async function initAttendances() {

    const search = q("attSearch");
    const employeeId = q("attEmployeeId");
    const status = q("attStatus");
    const month = q("attMonth");
    const year = q("attYear");
    const pageSize = q("attPageSize");
    const applyBtn = q("attApply");
    const refreshBtn = q("attRefreshBtn");
    const resetBtn = q("attReset");
    const createBtn = q("attCreateBtn");
    const adjSearch = q("adjSearch");
    const adjEmployeeId = q("adjEmployeeId");
    const adjDepartmentId = q("adjDepartmentId");
    const adjStatus = q("adjStatus");
    const adjMonth = q("adjMonth");
    const adjYear = q("adjYear");
    const adjWorkDate = q("adjWorkDate");
    const adjPageSize = q("adjPageSize");
    const adjApplyBtn = q("adjApply");
    const adjRefreshBtn = q("adjRefreshBtn");
    const adjResetBtn = q("adjReset");
    const adjExportBtn = q("adjExportBtn");
    const attExportFormat = q("attExportFormat");
    const attExportAttendanceBtn = q("attExportAttendanceBtn");
    const attExportAdjustmentsBtn = q("attExportAdjustmentsBtn");
    const attExportAdjustmentAuditBtn = q("attExportAdjustmentAuditBtn");
    const attPrintBtn = q("attPrintBtn");
    const attExportMessage = q("attExportMessage");
    const now = new Date();
    if (month) month.value = now.getMonth() + 1;
    if (year) year.value = now.getFullYear();
    if (adjMonth) adjMonth.value = now.getMonth() + 1;
    if (adjYear) adjYear.value = now.getFullYear();

    let departmentComparison = [];
    let monthlyTrends = [];
    let adjustmentAudit = [];

    function renderAttendancesChart(key, canvasId, config) {
      const el = q(canvasId);
      if (!el || typeof Chart === "undefined") return;
      state.charts[key]?.destroy?.();
      state.charts[key] = new Chart(el, config);
    }

    function getFilteredAttendances() {
      const term = (search?.value || "").trim().toLowerCase();
      let items = [...state.attendances];
      if (term) {
        items = items.filter(x =>
          [x.employeeCode, x.fullName, x.status, x.note, x.departmentName, x.positionName]
            .filter(Boolean).join(" ").toLowerCase().includes(term)
        );
      }
      return items;
    }

    function getFilteredAdjustments() {
      const term = (adjSearch?.value || "").trim().toLowerCase();
      let items = [...(state.attendanceAdjustments || [])];
      if (term) {
        items = items.filter(x =>
          [x.fullName, x.employeeCode, x.departmentName, x.status, x.requestedStatus, x.reason, x.reviewNote]
            .filter(Boolean).join(" ").toLowerCase().includes(term)
        );
      }
      return items;
    }

    function updateAttendanceExportScope() {
      const scopeEl = q("attExportScope");
      if (!scopeEl) return;
      const labelMonth = month?.value ? String(month.value).padStart(2, "0") : "--";
      const labelYear = year?.value || "----";
      const labelStatus = status?.value || "All statuses";
      scopeEl.value = `Attendance month ${labelMonth}/${labelYear} · ${labelStatus}`;
    }

    function setAttendanceKpiCards(items, adjustments) {
      const reliable = items.filter(x => ["present", "remote"].includes(String(x.status || "").toLowerCase())).length;
      const late = items.filter(x => String(x.status || "").toLowerCase() === "late").length;
      const absent = items.filter(x => String(x.status || "").toLowerCase() === "absent").length;
      const avgHours = items.length ? items.reduce((sum, x) => sum + Number(x.workingHours || 0), 0) / items.length : 0;
      const pendingAdjustments = adjustments.filter(x => String(x.status || "").toLowerCase() === "pending").length;
      const topDepartment = departmentComparison.length ? [...departmentComparison].sort((a, b) => (b.attendanceRecords || 0) - (a.attendanceRecords || 0))[0] : null;
      const focusText = pendingAdjustments
        ? `${pendingAdjustments} correction request(s) still need review.`
        : late + absent
          ? `${late + absent} flagged record(s) need monitoring.`
          : topDepartment
            ? `${topDepartment.departmentName} currently carries the heaviest attendance volume.`
            : "Attendance review snapshot";
      const setText = (id, value) => { const el = q(id); if (el) el.textContent = value; };
      setText("attKpiRecords", items.length);
      setText("attKpiReliable", reliable);
      setText("attKpiFlags", late + absent);
      setText("attKpiAverageHours", `${avgHours.toFixed(1)}h`);
      setText("attKpiPendingAdjustments", pendingAdjustments);
      setText("attKpiFocus", focusText);
    }

    function renderAttendanceHighlights(items, adjustments) {
      const highlightHost = q("attHighlights");
      const notesHost = q("attComparisonNotes");
      const late = items.filter(x => String(x.status || "").toLowerCase() === "late").length;
      const absent = items.filter(x => String(x.status || "").toLowerCase() === "absent").length;
      const reliable = items.filter(x => ["present", "remote"].includes(String(x.status || "").toLowerCase())).length;
      const pendingAdjustments = adjustments.filter(x => String(x.status || "").toLowerCase() === "pending").length;
      const reviewedAdjustments = adjustments.filter(x => !!x.reviewedAt).length;
      const topDepartment = departmentComparison.length ? [...departmentComparison].sort((a, b) => (b.pendingAdjustmentCount || 0) - (a.pendingAdjustmentCount || 0))[0] : null;
      if (highlightHost) {
        const itemsMarkup = [
          { icon: "bi-check2-circle", label: "Reliable attendance dominates", note: `${reliable} reliable day(s) are visible after the current filters were applied.` },
          { icon: "bi-exclamation-triangle", label: "Risk monitoring", note: `${late} late and ${absent} absent record(s) appear in the current slice.` },
          { icon: "bi-arrow-repeat", label: "Workflow pressure", note: `${pendingAdjustments} adjustment request(s) are still pending, while ${reviewedAdjustments} have already been reviewed.` },
          { icon: "bi-diagram-3", label: "Department comparison", note: topDepartment ? `${topDepartment.departmentName} currently has the largest correction backlog with ${topDepartment.pendingAdjustmentCount || 0} pending request(s).` : "Department comparison will populate when data becomes available." }
        ];
        highlightHost.innerHTML = itemsMarkup.map(item => `
          <div class="inline-metric-item">
            <i class="bi ${esc(item.icon)}"></i>
            <div><strong>${esc(item.label)}</strong><span>${esc(item.note)}</span></div>
          </div>`).join("");
      }
      if (notesHost) {
        notesHost.textContent = topDepartment
          ? `${topDepartment.departmentName} leads with ${topDepartment.attendanceRecords || 0} attendance record(s) and ${topDepartment.pendingAdjustmentCount || 0} pending adjustment(s) in the selected month.`
          : "Department comparison will update after attendance and adjustment data has loaded.";
      }
    }

    function renderAttendanceComparison() {
      const comparison = [...departmentComparison].sort((a, b) => (b.attendanceRecords || 0) - (a.attendanceRecords || 0));
      const topItems = comparison.slice(0, 6);
      renderAttendancesChart("attendanceOpsDepartment", "attDepartmentChart", {
        type: "bar",
        data: {
          labels: topItems.map(x => x.departmentName || "Unknown"),
          datasets: [
            { label: "Attendance Records", data: topItems.map(x => x.attendanceRecords || 0), backgroundColor: "rgba(37,99,235,.82)", borderRadius: 12, maxBarThickness: 34 },
            { label: "Pending Adjustments", data: topItems.map(x => x.pendingAdjustmentCount || 0), backgroundColor: "rgba(245,158,11,.82)", borderRadius: 12, maxBarThickness: 34 }
          ]
        },
        options: {
          maintainAspectRatio: false,
          plugins: { legend: { position: "bottom" } },
          scales: { y: { beginAtZero: true, grid: { color: "rgba(148,163,184,.16)" } }, x: { grid: { display: false } } }
        }
      });
      const body = q("attDepartmentBody");
      if (body) {
        body.innerHTML = comparison.length ? comparison.map(item => {
          const presentRate = item.attendanceRecords ? Math.round(((item.presentCount || 0) / item.attendanceRecords) * 100) : 0;
          return `
            <tr>
              <td><strong>${esc(item.departmentName || "-")}</strong><br><small class="text-muted">${esc(item.departmentCode || "-")}</small></td>
              <td>${item.headcount || 0}</td>
              <td>${item.attendanceRecords || 0}</td>
              <td>${presentRate}%</td>
              <td>${item.lateCount || 0}</td>
              <td>${item.absentCount || 0}</td>
              <td>${item.pendingAdjustmentCount || 0}</td>
              <td>${item.payrollCoverage || 0}</td>
            </tr>`;
        }).join("") : `<tr><td colspan="8"><div class="empty-state">No department comparison data is available for the selected month.</div></td></tr>`;
      }
    }

    function renderAttendanceOperationsDashboard() {
      const items = getFilteredAttendances();
      const adjustments = getFilteredAdjustments();
      const present = items.filter(x => String(x.status).toLowerCase() === "present").length;
      const late = items.filter(x => String(x.status).toLowerCase() === "late").length;
      const leaveCnt = items.filter(x => String(x.status).toLowerCase() === "leave").length;
      const absent = items.filter(x => String(x.status).toLowerCase() === "absent").length;
      const remote = items.filter(x => String(x.status).toLowerCase() === "remote").length;
      const pendingAdjustments = adjustments.filter(x => String(x.status).toLowerCase() === "pending").length;
      const approvedAdjustments = adjustments.filter(x => String(x.status).toLowerCase() === "approved").length;
      const rejectedAdjustments = adjustments.filter(x => String(x.status).toLowerCase() === "rejected").length;
      const avgHours = items.length ? items.reduce((sum, x) => sum + Number(x.workingHours || 0), 0) / items.length : 0;

      setStatGrid("attQuickStats", [["Total Records", items.length], ["Present", present], ["Late", late], ["Leave", leaveCnt], ["Absent", absent], ["Remote", remote]]);
      setStatGrid("attAdjustmentStats", [["All Requests", adjustments.length], ["Pending", pendingAdjustments], ["Approved", approvedAdjustments], ["Rejected", rejectedAdjustments], ["HR/Admin Reviewed", adjustments.filter(x => !!x.reviewedAt).length], ["Departments", new Set(adjustments.map(x => x.departmentName).filter(Boolean)).size]]);
      setAttendanceKpiCards(items, adjustments);
      renderAttendanceHighlights(items, adjustments);
      renderAttendanceComparison();

      renderAttendancesChart("attendanceOpsStatus", "attStatusChart", {
        type: "doughnut",
        data: { labels: ["Present", "Late", "Absent", "Leave", "Remote"], datasets: [{ data: [present, late, absent, leaveCnt, remote], backgroundColor: ["rgba(37,99,235,.82)", "rgba(245,158,11,.82)", "rgba(239,68,68,.82)", "rgba(139,92,246,.82)", "rgba(16,185,129,.82)"], borderWidth: 0 }] },
        options: { maintainAspectRatio: false, plugins: { legend: { position: "bottom" } } }
      });

      renderAttendancesChart("attendanceOpsAdjustment", "attAdjustmentChart", {
        type: "doughnut",
        data: { labels: ["Pending", "Approved", "Rejected"], datasets: [{ data: [pendingAdjustments, approvedAdjustments, rejectedAdjustments], backgroundColor: ["rgba(245,158,11,.82)", "rgba(34,197,94,.82)", "rgba(239,68,68,.82)"], borderWidth: 0 }] },
        options: { maintainAspectRatio: false, plugins: { legend: { position: "bottom" } } }
      });

      renderAttendancesChart("attendanceOpsTrend", "attTrendChart", {
        type: "line",
        data: {
          labels: monthlyTrends.map(x => x.periodLabel || `${x.month}/${x.year}`),
          datasets: [
            { label: "Attendance Records", data: monthlyTrends.map(x => x.attendanceRecords || 0), borderColor: "rgba(37,99,235,.92)", backgroundColor: "rgba(37,99,235,.18)", tension: .35, fill: true, pointRadius: 4 },
            { label: "Late Records", data: monthlyTrends.map(x => x.lateCount || 0), borderColor: "rgba(245,158,11,.92)", backgroundColor: "rgba(245,158,11,.12)", tension: .35, fill: false, pointRadius: 4 }
          ]
        },
        options: { maintainAspectRatio: false, plugins: { legend: { position: "bottom" } }, scales: { y: { beginAtZero: true, grid: { color: "rgba(148,163,184,.16)" } }, x: { grid: { display: false } } } }
      });

      renderActivityItems("attAuditFeed", (adjustmentAudit || []).slice(0, 8).map(item => ({
        title: `${item.actionType || "Adjustment audit"} · ${item.currentStatus || "-"}`,
        subtitle: `${item.employeeFullName || item.employeeCode || "-"} · ${dt(item.workDate)} · ${dtTime(item.createdAt)}`,
        meta: item.note || "No review note was recorded.",
        code: item.performedByUsername || ""
      })), "No adjustment audit events are available yet.");

      updateAttendanceExportScope();
    }

    async function load() {
      try {
        clearError();
        await ensureDepartmentsLoaded();
        syncDepartmentFilterOptions();

        const requestYear = Number(year?.value || now.getFullYear());
        const requestMonth = Number(month?.value || (now.getMonth() + 1));
        const attendanceParams = buildQuery({ employeeId: employeeId?.value, status: status?.value, month: requestMonth, year: requestYear });
        const adjustmentParams = buildQuery({ status: adjStatus?.value, employeeId: adjEmployeeId?.value, departmentId: adjDepartmentId?.value, month: adjWorkDate?.value ? null : adjMonth?.value, year: adjWorkDate?.value ? null : adjYear?.value, workDate: adjWorkDate?.value, search: adjSearch?.value?.trim() || null });

        const [items, requests, comparison, trends, audit] = await Promise.all([
          api(endpoints.attendances(attendanceParams)),
          api(`/api/Attendances/adjustment-requests${adjustmentParams ? `?${adjustmentParams}` : ""}`),
          api(`/api/Reports/department-comparison?${buildQuery({ year: requestYear, month: requestMonth })}`).catch(() => []),
          api(`/api/Reports/monthly-trends?${buildQuery({ year: requestYear, month: requestMonth, monthsBack: 6 })}`).catch(() => []),
          api(`/api/Attendances/adjustment-history/recent?take=10`).catch(() => [])
        ]);
        state.attendances = Array.isArray(items) ? items : [];
        state.attendanceAdjustments = Array.isArray(requests) ? requests : [];
        departmentComparison = Array.isArray(comparison) ? comparison : [];
        monthlyTrends = Array.isArray(trends) ? trends : [];
        adjustmentAudit = Array.isArray(audit) ? audit : [];
        renderAttendances();
        renderAdjustmentRequests();
        renderAttendanceOperationsDashboard();
      } catch (err) {
        showError(err.message || "Failed to load attendances.");
      }
    }

    function attendanceFormHtml(item = null) {
      const statusOptions = ["Present", "Late", "Absent", "Leave", "Remote"].map(x => `<option value="${x}" ${String(item?.status || "Present") === x ? "selected" : ""}>${x}</option>`).join("");
      return `
        <form id="attendanceForm" class="row g-3">
          <div class="col-md-6"><label class="form-label">Employee ID</label><input id="attendanceEmployeeIdInput" type="number" class="form-control" value="${esc(item?.employeeId ?? employeeId?.value ?? "")}" required /></div>
          <div class="col-md-6"><label class="form-label">Work Date</label><input id="attendanceWorkDateInput" type="date" class="form-control" value="${fmtInputDate(item?.workDate)}" required /></div>
          <div class="col-md-6"><label class="form-label">Check In Time</label><input id="attendanceCheckInInput" type="time" class="form-control" value="${fmtInputTime(item?.checkInTime)}" required /></div>
          <div class="col-md-6"><label class="form-label">Check Out Time</label><input id="attendanceCheckOutInput" type="time" class="form-control" value="${fmtInputTime(item?.checkOutTime)}" /></div>
          <div class="col-md-6"><label class="form-label">Status</label><select id="attendanceStatusInput" class="form-select">${statusOptions}</select></div>
          <div class="col-12"><label class="form-label">Note</label><textarea id="attendanceNoteInput" class="form-control" rows="4">${esc(item?.note || "")}</textarea></div>
          <div class="col-12"><div id="attendanceFormMessage" class="small text-danger"></div></div>
        </form>`;
    }

    async function openAttendanceModal(item = null) {
      const submitBtn = await openFormModal({ title: item ? "Edit Attendance" : "Create Attendance", bodyHtml: attendanceFormHtml(item), submitText: item ? "Save changes" : "Create attendance" });
      submitBtn?.addEventListener("click", async () => {
        const message = q("attendanceFormMessage");
        if (message) message.textContent = "";
        const workDateValue = q("attendanceWorkDateInput")?.value;
        const checkInValue = q("attendanceCheckInInput")?.value;
        const checkOutValue = q("attendanceCheckOutInput")?.value;
        const payload = {
          employeeId: Number(q("attendanceEmployeeIdInput")?.value || 0),
          workDate: workDateValue,
          checkInTime: combineLocalDateTime(workDateValue, checkInValue),
          checkOutTime: checkOutValue ? combineLocalDateTime(workDateValue, checkOutValue) : null,
          status: q("attendanceStatusInput")?.value,
          note: q("attendanceNoteInput")?.value?.trim() || null
        };
        try {
          if (item) await api(`/api/Attendances/${item.id}`, "PUT", payload);
          else await api("/api/Attendances", "POST", payload);
          closeSharedModal();
          await load();
        } catch (err) {
          if (message) message.textContent = err.message || "Failed to save attendance.";
        }
      }, { once: true });
    }

    async function removeAttendance(item) {
      if (!confirm(`Delete attendance ${item.fullName} on ${dt(item.workDate)}?`)) return;
      try { await api(`/api/Attendances/${item.id}`, "DELETE"); await load(); }
      catch (err) { showError(err.message || "Failed to delete attendance."); }
    }

    function renderAttendances() {
      state.paging.attendances.pageSize = Number(pageSize?.value || 10);
      const items = getFilteredAttendances();
      const pageItems = slicePaged(items, "attendances");
      const body = q("attBody");
      body.innerHTML = pageItems.length ? pageItems.map(x => `
        <tr>
          <td>${dt(x.workDate)}</td>
          <td><strong>${esc(x.employeeCode || "-")}</strong></td>
          <td>${esc(x.fullName || "-")}</td>
          <td>${esc(x.departmentName || "-")}</td>
          <td><span class="badge-soft ${statusBadgeClass(x.status)}">${esc(x.status)}</span></td>
          <td>${x.checkInTime ? dtTime(x.checkInTime) : "-"}</td>
          <td>${x.checkOutTime ? dtTime(x.checkOutTime) : "-"}</td>
          <td>${esc(x.workingHours ?? "-")}</td>
          <td>${esc(x.note || "-")}</td>
          <td><div class="action-group"><button class="btn btn-sm btn-outline-primary" data-view-att-id="${x.id}">View</button><button class="btn btn-sm btn-outline-secondary" data-edit-att-id="${x.id}">Edit</button>${isAdmin() ? `<button class="btn btn-sm btn-outline-danger" data-delete-att-id="${x.id}">Delete</button>` : ""}</div></td>
        </tr>`).join("") : `<tr><td colspan="10"><div class="empty-state">No attendance records match the current filters.</div></td></tr>`;
      renderPaging("attPaging", "attendances", items.length, renderAttendances);
      body.querySelectorAll("[data-view-att-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.attendances.find(x => String(x.id) === String(btn.dataset.viewAttId));
        if (!item) return;
        await openDetailModal("Attendance Details", `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Employee</label><input class="form-control" value="${esc(item.fullName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Employee Code</label><input class="form-control" value="${esc(item.employeeCode || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Department</label><input class="form-control" value="${esc(item.departmentName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Status</label><input class="form-control" value="${esc(item.status || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Work Date</label><input class="form-control" value="${dt(item.workDate)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Hours</label><input class="form-control" value="${esc(item.workingHours ?? "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Check In</label><input class="form-control" value="${item.checkInTime ? dtTime(item.checkInTime) : "-"}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Check Out</label><input class="form-control" value="${item.checkOutTime ? dtTime(item.checkOutTime) : "-"}" readonly /></div>
            <div class="col-12"><label class="form-label">Note</label><textarea class="form-control" readonly>${esc(item.note || "")}</textarea></div>
          </div>
        `, false);
      }));
      body.querySelectorAll("[data-edit-att-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.attendances.find(x => String(x.id) === String(btn.dataset.editAttId));
        if (item) await openAttendanceModal(item);
      }));
      body.querySelectorAll("[data-delete-att-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.attendances.find(x => String(x.id) === String(btn.dataset.deleteAttId));
        if (item) await removeAttendance(item);
      }));
    }

    function renderAdjustmentRequests() {
      state.paging.attendanceAdjustments.pageSize = Number(adjPageSize?.value || 8);
      const items = getFilteredAdjustments();
      const pageItems = slicePaged(items, "attendanceAdjustments");
      const body = q("attAdjustmentBody");
      if (!body) return;
      body.innerHTML = pageItems.length ? pageItems.map(x => `
        <tr>
          <td>${dtTime(x.createdAt)}</td>
          <td><strong>${esc(x.fullName || "-")}</strong><br><small class="text-muted">${esc(x.employeeCode || "-")}</small></td>
          <td>${esc(x.departmentName || "-")}</td>
          <td>${dt(x.workDate)}</td>
          <td><span class="badge-soft ${statusBadgeClass(x.requestedStatus)}">${esc(x.requestedStatus || "-")}</span></td>
          <td>${x.requestedCheckInTime ? dtTime(x.requestedCheckInTime) : "-"}<br>${x.requestedCheckOutTime ? dtTime(x.requestedCheckOutTime) : "-"}</td>
          <td>${esc(x.reason || "-")}</td>
          <td><span class="badge-soft ${statusBadgeClass(x.status)}">${esc(x.status || "-")}</span></td>
          <td>${x.reviewedAt ? `<div><strong>${esc(x.reviewedByUsername || "-")}</strong></div><small class="text-muted">${dtTime(x.reviewedAt)}</small>` : '<span class="text-muted">Pending review</span>'}</td>
          <td><div class="action-group flex-column align-items-stretch"><button class="btn btn-sm btn-outline-primary" data-view-adjustment-id="${x.id}">View</button>${String(x.status).toLowerCase() === "pending" ? `<button class="btn btn-sm btn-success" data-approve-adjustment-id="${x.id}">Approve</button><button class="btn btn-sm btn-danger" data-reject-adjustment-id="${x.id}">Reject</button>` : ""}</div></td>
        </tr>`).join("") : `<tr><td colspan="10"><div class="empty-state">No adjustment requests found.</div></td></tr>`;
      renderPaging("attAdjustmentPaging", "attendanceAdjustments", items.length, renderAdjustmentRequests);
      body.querySelectorAll("[data-view-adjustment-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.attendanceAdjustments.find(x => String(x.id) === String(btn.dataset.viewAdjustmentId));
        if (!item) return;
        const history = await fetchAdjustmentHistory(item.id);
        const historyMarkup = history.length ? history.map(entry => `<div class="audit-item"><strong>${esc(entry.actionType || "-")}</strong><span>${esc(entry.performedByUsername || "System")} · ${dtTime(entry.createdAt)}</span><small>${esc(entry.previousStatus || "-")} → ${esc(entry.newStatus || entry.currentStatus || "-")} · ${esc(entry.note || "No note recorded.")}</small></div>`).join("") : renderAuditTrail(item);
        await openDetailModal("Attendance Adjustment Request", `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Employee</label><input class="form-control" value="${esc(item.fullName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Employee Code</label><input class="form-control" value="${esc(item.employeeCode || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Department</label><input class="form-control" value="${esc(item.departmentName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Work Date</label><input class="form-control" value="${dt(item.workDate)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Requested Status</label><input class="form-control" value="${esc(item.requestedStatus || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Request Status</label><input class="form-control" value="${esc(item.status || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Requested Check In</label><input class="form-control" value="${item.requestedCheckInTime ? dtTime(item.requestedCheckInTime) : "-"}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Requested Check Out</label><input class="form-control" value="${item.requestedCheckOutTime ? dtTime(item.requestedCheckOutTime) : "-"}" readonly /></div>
            <div class="col-12"><label class="form-label">Reason</label><textarea class="form-control" readonly>${esc(item.reason || "")}</textarea></div>
            <div class="col-12"><label class="form-label">Audit trail</label><div class="audit-trail-card">${historyMarkup}</div></div>
          </div>
        `, false);
      }));
      body.querySelectorAll("[data-approve-adjustment-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.attendanceAdjustments.find(x => String(x.id) === String(btn.dataset.approveAdjustmentId));
        if (item) await openAdjustmentReviewModal(item, "approve");
      }));
      body.querySelectorAll("[data-reject-adjustment-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.attendanceAdjustments.find(x => String(x.id) === String(btn.dataset.rejectAdjustmentId));
        if (item) await openAdjustmentReviewModal(item, "reject");
      }));
    }

    async function openAdjustmentReviewModal(item, action) {
      const submitBtn = await openFormModal({
        title: action === "approve" ? "Approve Attendance Adjustment" : "Reject Attendance Adjustment",
        bodyHtml: `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Employee</label><input class="form-control" value="${esc(item.fullName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Work Date</label><input class="form-control" value="${dt(item.workDate)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Requested Status</label><input class="form-control" value="${esc(item.requestedStatus || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Requested Check In</label><input class="form-control" value="${item.requestedCheckInTime ? dtTime(item.requestedCheckInTime) : "-"}" readonly /></div>
            <div class="col-12"><label class="form-label">Reason</label><textarea class="form-control" readonly>${esc(item.reason || "")}</textarea></div>
            <div class="col-12"><label class="form-label">${action === "approve" ? "Approval note (optional)" : "Reject reason"}</label><textarea id="attendanceAdjustmentReviewNote" class="form-control" rows="4" placeholder="${action === "approve" ? "Optional review note" : "Required reject reason"}"></textarea></div>
            <div class="col-12"><div id="attendanceAdjustmentReviewMessage" class="small text-danger"></div></div>
          </div>`,
        submitText: action === "approve" ? "Approve request" : "Reject request"
      });
      submitBtn?.addEventListener("click", async () => {
        const note = q("attendanceAdjustmentReviewNote")?.value?.trim() || "";
        const message = q("attendanceAdjustmentReviewMessage");
        if (message) message.textContent = "";
        if (action === "reject" && !note) { if (message) message.textContent = "Reject reason is required."; return; }
        try { await api(`/api/Attendances/adjustment-requests/${item.id}/${action}`, "PUT", { reviewNote: note }); closeSharedModal(); await load(); }
        catch (err) { if (message) message.textContent = err.message || `Failed to ${action} adjustment request.`; }
      }, { once: true });
    }

    function openAttendancePrintSnapshot() {
      const attendanceItems = getFilteredAttendances();
      const adjustmentItems = getFilteredAdjustments();
      const title = "Attendance Operations Snapshot";
      const subtitle = `Month ${String(month?.value || "").padStart(2, "0")}/${year?.value || ""} · ${status?.value || "All statuses"}`;
      const html = `<!DOCTYPE html><html><head><meta charset="UTF-8"><title>${esc(title)}</title><style>body{font-family:Arial,sans-serif;padding:24px;color:#0f172a}h1{margin:0 0 6px;font-size:24px}p{margin:0 0 14px;color:#475569}h2{margin:26px 0 10px;font-size:18px}table{width:100%;border-collapse:collapse;font-size:12px;margin-bottom:20px}th,td{border:1px solid #cbd5e1;padding:8px;text-align:left;vertical-align:top}th{background:#eff6ff}.meta{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px;margin:18px 0 22px}.card{border:1px solid #cbd5e1;border-radius:12px;padding:12px}</style></head><body><h1>${esc(title)}</h1><p>${esc(subtitle)}</p><div class="meta"><div class="card"><strong>Attendance records</strong><div>${attendanceItems.length}</div></div><div class="card"><strong>Pending adjustments</strong><div>${adjustmentItems.filter(x => String(x.status || "").toLowerCase() === "pending").length}</div></div><div class="card"><strong>Departments in slice</strong><div>${new Set(attendanceItems.map(x => x.departmentName).filter(Boolean)).size}</div></div></div><h2>Attendance Records</h2><table><thead><tr><th>Work Date</th><th>Employee</th><th>Department</th><th>Status</th><th>Hours</th><th>Note</th></tr></thead><tbody>${attendanceItems.length ? attendanceItems.map(x => `<tr><td>${dt(x.workDate)}</td><td>${esc(x.fullName || "-")} (${esc(x.employeeCode || "-")})</td><td>${esc(x.departmentName || "-")}</td><td>${esc(x.status || "-")}</td><td>${esc(x.workingHours ?? "-")}</td><td>${esc(x.note || "-")}</td></tr>`).join("") : `<tr><td colspan="6">No attendance records available.</td></tr>`}</tbody></table><h2>Adjustment Queue</h2><table><thead><tr><th>Created</th><th>Employee</th><th>Department</th><th>Work Date</th><th>Requested</th><th>Status</th></tr></thead><tbody>${adjustmentItems.length ? adjustmentItems.map(x => `<tr><td>${dtTime(x.createdAt)}</td><td>${esc(x.fullName || "-")} (${esc(x.employeeCode || "-")})</td><td>${esc(x.departmentName || "-")}</td><td>${dt(x.workDate)}</td><td>${esc(x.requestedStatus || "-")}</td><td>${esc(x.status || "-")}</td></tr>`).join("") : `<tr><td colspan="6">No adjustment requests available.</td></tr>`}</tbody></table></body></html>`;
      const popup = window.open("", "_blank", "width=1180,height=780");
      if (!popup) return;
      popup.document.open(); popup.document.write(html); popup.document.close(); popup.focus(); popup.print();
    }

    async function runAttendanceExport(url, message, fallbackName) {
      if (attExportMessage) attExportMessage.textContent = "";
      try { await downloadFileFromApi(url, fallbackName); if (attExportMessage) attExportMessage.textContent = message; }
      catch (err) { showError(err.message || "Failed to export attendance report."); }
    }

    function selectedExportFormat() { return attExportFormat?.value || "csv"; }

    search?.addEventListener("input", () => { state.paging.attendances.page = 1; renderAttendances(); renderAttendanceOperationsDashboard(); });
    pageSize?.addEventListener("change", () => { state.paging.attendances.page = 1; renderAttendances(); renderAttendanceOperationsDashboard(); });
    applyBtn?.addEventListener("click", () => { state.paging.attendances.page = 1; load(); });
    refreshBtn?.addEventListener("click", load);
    createBtn?.addEventListener("click", () => openAttendanceModal());
    resetBtn?.addEventListener("click", () => { if (search) search.value = ""; if (employeeId) employeeId.value = ""; if (status) status.value = ""; if (month) month.value = now.getMonth() + 1; if (year) year.value = now.getFullYear(); state.paging.attendances.page = 1; load(); });

    [adjSearch, adjEmployeeId].forEach(el => el?.addEventListener("input", () => { state.paging.attendanceAdjustments.page = 1; renderAdjustmentRequests(); renderAttendanceOperationsDashboard(); }));
    [adjStatus, adjDepartmentId, adjMonth, adjYear, adjWorkDate, adjPageSize].forEach(el => el?.addEventListener("change", () => { state.paging.attendanceAdjustments.page = 1; renderAdjustmentRequests(); renderAttendanceOperationsDashboard(); }));
    adjApplyBtn?.addEventListener("click", () => { state.paging.attendanceAdjustments.page = 1; load(); });
    adjRefreshBtn?.addEventListener("click", load);
    adjResetBtn?.addEventListener("click", () => { if (adjSearch) adjSearch.value = ""; if (adjEmployeeId) adjEmployeeId.value = ""; if (adjDepartmentId) adjDepartmentId.value = ""; if (adjStatus) adjStatus.value = ""; if (adjWorkDate) adjWorkDate.value = ""; if (adjMonth) adjMonth.value = now.getMonth() + 1; if (adjYear) adjYear.value = now.getFullYear(); if (adjPageSize) adjPageSize.value = "8"; state.paging.attendanceAdjustments.page = 1; load(); });

    adjExportBtn?.addEventListener("click", async () => {
      const exportParams = buildQuery({
        status: adjStatus?.value,
        employeeId: adjEmployeeId?.value,
        departmentId: adjDepartmentId?.value,
        month: adjWorkDate?.value ? null : adjMonth?.value,
        year: adjWorkDate?.value ? null : adjYear?.value,
        workDate: adjWorkDate?.value,
        search: adjSearch?.value?.trim() || null,
        format: 'csv'
      });
      await runAttendanceExport(`/api/Reports/attendance-adjustments/export?${exportParams}`, "Attendance adjustment queue exported successfully.", `attendance-adjustments-${Date.now()}.csv`);
    });

    attExportAttendanceBtn?.addEventListener("click", async () => {
      const format = selectedExportFormat();
      const exportParams = buildQuery({ status: status?.value, employeeId: employeeId?.value, month: month?.value, year: year?.value, search: search?.value?.trim() || null, format });
      await runAttendanceExport(`/api/Reports/attendances/export?${exportParams}`, "Attendance records exported successfully.", `attendance-records-${Date.now()}.${format === "xlsx" ? "xlsx" : format}`);
    });
    attExportAdjustmentsBtn?.addEventListener("click", async () => {
      const format = selectedExportFormat();
      const exportParams = buildQuery({ status: adjStatus?.value, employeeId: adjEmployeeId?.value, departmentId: adjDepartmentId?.value, month: adjWorkDate?.value ? null : adjMonth?.value, year: adjWorkDate?.value ? null : adjYear?.value, workDate: adjWorkDate?.value, search: adjSearch?.value?.trim() || null, format });
      await runAttendanceExport(`/api/Reports/attendance-adjustments/export?${exportParams}`, "Attendance adjustment queue exported successfully.", `attendance-adjustments-${Date.now()}.${format === "xlsx" ? "xlsx" : format}`);
    });
    attExportAdjustmentAuditBtn?.addEventListener("click", async () => {
      const format = selectedExportFormat();
      const exportParams = buildQuery({ year: adjYear?.value || year?.value, month: adjMonth?.value || month?.value, employeeId: adjEmployeeId?.value || employeeId?.value || null, format });
      await runAttendanceExport(`/api/Reports/attendance-adjustment-audit/export?${exportParams}`, "Attendance adjustment audit trail exported successfully.", `attendance-adjustment-audit-${Date.now()}.${format === "xlsx" ? "xlsx" : format}`);
    });
    attPrintBtn?.addEventListener("click", openAttendancePrintSnapshot);

    load();
  }

  async function initPayrolls() {

    const search = q("paySearch");
    const employeeId = q("payEmployeeId");
    const month = q("payMonth");
    const year = q("payYear");
    const pageSize = q("payPageSize");
    const applyBtn = q("payApply");
    const resetBtn = q("payReset");
    const generateBtn = q("payGenerateAll");
    const generateOneBtn = q("payGenerateOne");
    const periodStatus = q("payPeriodStatus");
    const periodMeta = q("payPeriodMeta");
    const periodToggleBtn = q("payPeriodToggle");
    const aiTemplate = q("payAiTemplate");
    const aiDepartment = q("payAiDepartment");
    const aiPrompt = q("payAiPrompt");
    const aiRunBtn = q("payAiRun");
    const aiUseVisibleBtn = q("payAiUseVisible");
    const aiMessage = q("payAiMessage");
    const now = new Date();
    if (month) month.value = now.getMonth() + 1;
    if (year) year.value = now.getFullYear();

    async function setupAiPanel() {
      try {
        await ensureDepartmentsLoaded();
        await ensureAiTemplatesLoaded();
        populateDepartmentSelect('payAiDepartment', 'All departments');
        populateAiTemplateControls('payAiTemplate', 'payAiTemplateHelper', 'payAiPrompt');
        if (state.role === 'Manager' && aiDepartment) {
          aiDepartment.disabled = true;
          aiDepartment.parentElement?.querySelector('.form-text')?.remove();
        }
      } catch (err) {
        if (aiMessage) {
          aiMessage.className = 'small text-danger';
          aiMessage.textContent = err.message || 'Failed to load AI assistant controls.';
        }
      }
    }

    async function triggerAiSummary(useVisibleResults = false) {
      const selectedDepartment = aiDepartment?.value || '';
      const customPrompt = useVisibleResults && !aiPrompt?.value?.trim()
        ? `Summarize payroll anomalies for ${String(month?.value || '').padStart(2, '0')}/${year?.value || ''}. Focus on the currently visible payroll results after filters and search.`
        : (aiPrompt?.value?.trim() || '');
      await runAiPayrollSummary({
        month: month?.value || (now.getMonth() + 1),
        year: year?.value || now.getFullYear(),
        departmentId: selectedDepartment,
        templateKey: aiTemplate?.value || 'anomaly-overview',
        prompt: customPrompt,
        messageId: 'payAiMessage',
        resultId: 'payAiResult',
        scopeLabel: 'Payroll workspace assistant'
      });
    }

      async function refreshPeriodPanel() {
          if (!periodStatus || !year?.value) return;
          try {
              const periods = await api(`/api/Payrolls/periods?year=${year.value}`);
              const current = Array.isArray(periods)
                  ? periods.find(x =>
                      Number(x.payrollYear) === Number(year.value) &&
                      Number(x.payrollMonth) === Number(month?.value || 0))
                  : null;

              const locked = current?.isLocked === true;
              periodStatus.textContent = locked ? 'Locked' : 'Open';
              periodStatus.className = `status-pill ${locked ? 'status-rejected' : 'status-approved'}`;

              periodMeta.textContent = current
                  ? `${locked ? 'Locked' : 'Open'} period ${String(current.payrollMonth).padStart(2, '0')}/${current.payrollYear}${current.lockedByUsername ? ` · ${current.lockedByUsername}` : ''}${current.lockedAt ? ` · ${dtTime(current.lockedAt)}` : ''}`
                  : `No explicit period record yet for ${String(month?.value || '').padStart(2, '0')}/${year.value}.`;

              if (periodToggleBtn) periodToggleBtn.textContent = locked ? 'Unlock period' : 'Lock period';
              if (periodToggleBtn) periodToggleBtn.dataset.locked = locked ? 'true' : 'false';
          } catch (err) {
              if (periodStatus) periodStatus.textContent = 'Unavailable';
              if (periodMeta) periodMeta.textContent = err.message || 'Could not load payroll period status.';
          }
      }

      async function togglePeriodLock() {
          const currentLocked = periodToggleBtn?.dataset.locked === 'true';
          const note = prompt(currentLocked ? 'Optional unlock note' : 'Optional lock note', '') ?? '';
          try {
              await api(`/api/Payrolls/periods/${year?.value}/${month?.value}/lock`, 'PUT', {
                  isLocked: !currentLocked,
                  note
              });
              await refreshPeriodPanel();
              await load();
          } catch (err) {
              showError(err.message || 'Failed to update payroll period lock.');
          }
      }

    async function load() {
      try {
        clearError();
        const params = new URLSearchParams();
        if (employeeId?.value) params.set("employeeId", employeeId.value);
        if (month?.value) params.set("month", month.value);
        if (year?.value) params.set("year", year.value);
        const items = await api(endpoints.payrolls(params.toString()));
        state.payrolls = Array.isArray(items) ? items : [];
        renderPayrolls();
        await refreshPeriodPanel();
      } catch (err) {
        showError(err.message || "Failed to load payrolls.");
      }
    }

    async function generateAll() {
      const requestMonth = month?.value || (new Date().getMonth() + 1);
      const requestYear = year?.value || new Date().getFullYear();
      const overwrite = confirm("Overwrite existing payrolls for this month if they already exist?\nOK = overwrite, Cancel = keep existing.");
      try {
        await api("/api/Payrolls/generate-all", "POST", {
          month: Number(requestMonth),
          year: Number(requestYear),
          overwriteExisting: overwrite
        });
        await load();
        alert("Generate-all payroll request completed.");
      } catch (err) {
        showError(err.message || "Failed to generate payrolls.");
      }
    }

    async function generateOne() {
      const empId = prompt("Enter employee ID to generate payroll for:", employeeId?.value || "");
      if (!empId) return;
      const bonus = prompt("Bonus amount (optional)", "0") ?? "0";
      const deduction = prompt("Deduction amount (optional)", "0") ?? "0";
      try {
        await api("/api/Payrolls/generate", "POST", {
          employeeId: Number(empId),
          month: Number(month?.value || now.getMonth() + 1),
          year: Number(year?.value || now.getFullYear()),
          bonus: Number(bonus || 0),
          deduction: Number(deduction || 0)
        });
        await load();
      } catch (err) {
        showError(err.message || "Failed to generate payroll.");
      }
    }

    async function editPayroll(item) {
      const submitBtn = await openFormModal({
        title: `Update Payroll: ${item.employeeCode} - ${item.payrollMonth}/${item.payrollYear}`,
        bodyHtml: `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Employee</label><input class="form-control" value="${esc(item.fullName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Current Net Salary</label><input class="form-control" value="${money(item.netSalary)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Bonus</label><input class="form-control" id="payBonusInput" type="number" step="0.01" value="${item.bonus ?? 0}" /></div>
            <div class="col-md-6"><label class="form-label">Deduction</label><input class="form-control" id="payDeductionInput" type="number" step="0.01" value="${item.deduction ?? 0}" /></div>
            <div class="col-12"><div id="payrollFormMessage" class="small text-danger"></div></div>
          </div>`,
        submitText: "Update payroll"
      });
      submitBtn?.addEventListener("click", async () => {
        try {
          await api(`/api/Payrolls/${item.id}`, "PUT", {
            bonus: Number(q("payBonusInput")?.value || 0),
            deduction: Number(q("payDeductionInput")?.value || 0)
          });
          closeSharedModal();
          await load();
        } catch (err) {
          q("payrollFormMessage").textContent = err.message || "Failed to update payroll.";
        }
      }, { once: true });
    }

    async function deletePayroll(item) {
      if (!confirm(`Delete payroll ${item.payrollMonth}/${item.payrollYear} for ${item.fullName}?`)) return;
      try {
        await api(`/api/Payrolls/${item.id}`, "DELETE");
        await load();
      } catch (err) {
        showError(err.message || "Failed to delete payroll.");
      }
    }

    function renderPayrolls() {
      const term = (search?.value || "").trim().toLowerCase();
      state.paging.payrolls.pageSize = Number(pageSize?.value || 10);
      let items = [...state.payrolls];
      if (term) {
        items = items.filter(x =>
          [x.employeeCode, x.fullName, x.departmentName, x.positionName]
            .filter(Boolean).join(" ").toLowerCase().includes(term)
        );
      }
      const pageItems = slicePaged(items, "payrolls");
      const body = q("payBody");
      body.innerHTML = pageItems.length ? pageItems.map(x => `
        <tr>
          <td>${esc(x.payrollMonth)}/${esc(x.payrollYear)}</td>
          <td><strong>${esc(x.employeeCode || "-")}</strong></td>
          <td>${esc(x.fullName || "-")}</td>
          <td>${esc(x.departmentName || "-")}</td>
          <td>${money(x.baseSalary)}</td>
          <td>${money(x.bonus)}</td>
          <td>${money(x.deduction)}</td>
          <td><strong>${money(x.netSalary)}</strong></td>
          <td>${dtTime(x.generatedAt)}</td>
          <td>
            <div class="action-group">
              <button class="btn btn-sm btn-outline-primary" data-view-id="${x.id}">View</button>
              <button class="btn btn-sm btn-outline-secondary" data-edit-id="${x.id}">Adjust</button>
              ${state.role === "Admin" ? `<button class="btn btn-sm btn-outline-danger" data-delete-id="${x.id}">Delete</button>` : ""}
            </div>
          </td>
        </tr>`).join("") :
        `<tr><td colspan="10"><div class="empty-state">No payroll records match the current filters.</div></td></tr>`;
      renderPaging("payPaging", "payrolls", items.length, renderPayrolls);

      setStatGrid("payQuickStats", [
        ["Records", items.length],
        ["Total Net", money(items.reduce((a,b)=>a+(Number(b.netSalary)||0),0))],
        ["Total Bonus", money(items.reduce((a,b)=>a+(Number(b.bonus)||0),0))],
        ["Total Deduction", money(items.reduce((a,b)=>a+(Number(b.deduction)||0),0))]
      ]);

      body.querySelectorAll("[data-view-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.payrolls.find(x => String(x.id) === String(btn.dataset.viewId));
        if (!item) return;
        const history = await fetchPayrollHistory(item.id);
        await openDetailModal("Payroll Details", `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Employee</label><input class="form-control" value="${esc(item.fullName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Month</label><input class="form-control" value="${item.payrollMonth}/${item.payrollYear}" readonly /></div>
            <div class="col-md-4"><label class="form-label">Present</label><input class="form-control" value="${esc(item.presentDays)}" readonly /></div>
            <div class="col-md-4"><label class="form-label">Late</label><input class="form-control" value="${esc(item.lateDays)}" readonly /></div>
            <div class="col-md-4"><label class="form-label">Leave</label><input class="form-control" value="${esc(item.leaveDays)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Bonus</label><input class="form-control" value="${money(item.bonus)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Deduction</label><input class="form-control" value="${money(item.deduction)}" readonly /></div>
            <div class="col-12"><label class="form-label">Net Salary</label><input class="form-control" value="${money(item.netSalary)}" readonly /></div>
            <div class="col-12"><label class="form-label">Audit History</label><div class="audit-trail-card">${history.length ? history.map(h => `<div class="audit-item"><strong>${esc(h.actionType)}</strong><span>${esc((h.performedByUsername || 'System') + ' · ' + dtTime(h.createdAt))}</span><small>${esc(h.note || '')}</small></div>`).join('') : '<div class="empty-state">No payroll audit history yet.</div>'}</div></div>
          </div>`, false);
      }));
      body.querySelectorAll("[data-edit-id]").forEach(btn => btn.addEventListener("click", () => {
        const item = state.payrolls.find(x => String(x.id) === String(btn.dataset.editId));
        if (item) editPayroll(item);
      }));
      body.querySelectorAll("[data-delete-id]").forEach(btn => btn.addEventListener("click", () => {
        const item = state.payrolls.find(x => String(x.id) === String(btn.dataset.deleteId));
        if (item) deletePayroll(item);
      }));
    }

    search?.addEventListener("input", () => { state.paging.payrolls.page = 1; renderPayrolls(); });
    pageSize?.addEventListener("change", () => { state.paging.payrolls.page = 1; renderPayrolls(); });
    applyBtn?.addEventListener("click", () => { state.paging.payrolls.page = 1; load(); });
    resetBtn?.addEventListener("click", () => {
      search.value = "";
      employeeId.value = "";
      month.value = now.getMonth() + 1;
      year.value = now.getFullYear();
      state.paging.payrolls.page = 1;
      load();
    });
    generateBtn?.addEventListener("click", generateAll);
    generateOneBtn?.addEventListener("click", generateOne);
    periodToggleBtn?.addEventListener("click", togglePeriodLock);
    aiRunBtn?.addEventListener("click", () => triggerAiSummary(false).catch(() => { }));
    aiUseVisibleBtn?.addEventListener("click", () => triggerAiSummary(true).catch(() => { }));
    await setupAiPanel();
    renderAiSummaryResult('payAiResult', null, 'Payroll workspace assistant');
    await load();
  }

  async function initLeaves() {
    const search = q("leaveSearch");
    const status = q("leaveStatus");
    const type = q("leaveType");
    const pageSize = q("leavePageSize");
    const refresh = q("leaveRefresh");

    async function load() {
      try {
        clearError();
        const [all, pending] = await Promise.all([
          api(endpoints.leaves),
          api(endpoints.leavesPending).catch(() => [])
        ]);
        state.leaves = Array.isArray(all) ? all : [];
        state.leaveApprovers = Array.isArray(pending) ? pending : [];
        renderLeaves();
      } catch (err) {
        showError(err.message || "Failed to load leave requests.");
      }
    }

    async function openLeaveReviewModal(item, action) {
      const submitBtn = await openFormModal({
        title: action === "approve" ? "Approve Leave Request" : "Reject Leave Request",
        bodyHtml: `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Employee</label><input class="form-control" value="${esc(item.fullName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Employee Code</label><input class="form-control" value="${esc(item.employeeCode || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Leave Type</label><input class="form-control" value="${esc(item.leaveType || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Dates</label><input class="form-control" value="${dt(item.startDate)} - ${dt(item.endDate)}" readonly /></div>
            <div class="col-12"><label class="form-label">Reason</label><textarea class="form-control" readonly>${esc(item.reason || "")}</textarea></div>
            <div class="col-12"><label class="form-label">${action === "approve" ? "Approval note (optional)" : "Reject reason"}</label><textarea id="leaveReviewNoteInput" class="form-control" rows="4" placeholder="${action === "approve" ? "Optional approval note" : "Required reject reason"}"></textarea></div>
            <div class="col-12"><div id="leaveReviewMessage" class="small text-danger"></div></div>
          </div>`,
        submitText: action === "approve" ? "Approve" : "Reject"
      });

      submitBtn?.addEventListener("click", async () => {
        const message = q("leaveReviewMessage");
        if (message) message.textContent = "";
        const note = q("leaveReviewNoteInput")?.value?.trim() || "";
        if (action === "reject" && !note) {
          if (message) message.textContent = "Reject reason is required.";
          return;
        }
        try {
          if (action === "approve") {
            await api(`/api/LeaveRequests/${item.id}/approve`, "PUT", { approvalNote: note });
          } else {
            await api(`/api/LeaveRequests/${item.id}/reject`, "PUT", { rejectionReason: note });
          }
          closeSharedModal();
          await load();
        } catch (err) {
          if (message) message.textContent = err.message || `Failed to ${action} leave request.`;
        }
      }, { once: true });
    }

    function renderLeaves() {
      const term = (search?.value || "").trim().toLowerCase();
      const statusVal = (status?.value || "").toLowerCase();
      const typeVal = (type?.value || "").toLowerCase();
      state.paging.leaves.pageSize = Number(pageSize?.value || 10);
      let items = [...state.leaves];
      if (term) {
        items = items.filter(x =>
          [x.fullName, x.employeeCode, x.leaveType, x.reason, x.status]
            .filter(Boolean).join(" ").toLowerCase().includes(term)
        );
      }
      if (statusVal) items = items.filter(x => String(x.status || "").toLowerCase() === statusVal);
      if (typeVal) items = items.filter(x => String(x.leaveType || "").toLowerCase() === typeVal);

      const pageItems = slicePaged(items, "leaves");
      const body = q("leaveBody");
      body.innerHTML = pageItems.length ? pageItems.map(x => {
        const canReview = String(x.status || "").toLowerCase() === "pending";
        return `
        <tr>
          <td><strong>${esc(x.employeeCode || "-")}</strong></td>
          <td>${esc(x.fullName || "-")}</td>
          <td>${esc(x.leaveType || "-")}</td>
          <td>${dt(x.startDate)} - ${dt(x.endDate)}</td>
          <td>${esc(x.totalDays ?? "-")}</td>
          <td><span class="badge-soft ${statusBadgeClass(x.status)}">${esc(x.status)}</span></td>
          <td>${esc(x.reason || "-")}</td>
          <td>
            <div class="action-group">
              <button class="btn btn-sm btn-outline-primary" data-view-id="${x.id}">View</button>
              ${canReview ? `<button class="btn btn-sm btn-success" data-approve-id="${x.id}">Approve</button>
              <button class="btn btn-sm btn-danger" data-reject-id="${x.id}">Reject</button>` : ""}
            </div>
          </td>
        </tr>`;
      }).join("") : `<tr><td colspan="8"><div class="empty-state">No leave requests match the current filters.</div></td></tr>`;

      renderPaging("leavePaging", "leaves", items.length, renderLeaves);
      setStatGrid("leaveQuickStats", [
        ["Total", items.length],
        ["Pending", items.filter(x => String(x.status).toLowerCase() === "pending").length],
        ["Approved", items.filter(x => String(x.status).toLowerCase() === "approved").length],
        ["Rejected", items.filter(x => String(x.status).toLowerCase() === "rejected").length],
        ["Cancelled", items.filter(x => String(x.status).toLowerCase() === "cancelled").length]
      ]);

      body.querySelectorAll("[data-approve-id]").forEach(btn => btn.addEventListener("click", async () => { const item = state.leaves.find(x => String(x.id) === String(btn.dataset.approveId)); if (item) await openLeaveReviewModal(item, "approve"); }));
      body.querySelectorAll("[data-reject-id]").forEach(btn => btn.addEventListener("click", async () => { const item = state.leaves.find(x => String(x.id) === String(btn.dataset.rejectId)); if (item) await openLeaveReviewModal(item, "reject"); }));
      body.querySelectorAll("[data-view-id]").forEach(btn => btn.addEventListener("click", async () => {
        const item = state.leaves.find(x => String(x.id) === String(btn.dataset.viewId));
        if (!item) return;
        const history = await fetchLeaveHistory(item.id);
        await openDetailModal("Leave Request Details", `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Employee</label><input class="form-control" value="${esc(item.fullName || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Employee Code</label><input class="form-control" value="${esc(item.employeeCode || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Leave Type</label><input class="form-control" value="${esc(item.leaveType || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Status</label><input class="form-control" value="${esc(item.status || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Start Date</label><input class="form-control" value="${dt(item.startDate)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">End Date</label><input class="form-control" value="${dt(item.endDate)}" readonly /></div>
            <div class="col-12"><label class="form-label">Reason</label><textarea class="form-control" readonly>${esc(item.reason || "")}</textarea></div>
            <div class="col-md-6"><label class="form-label">Approved By</label><input class="form-control" value="${esc(item.approvedByUsername || "-")}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Reviewed At</label><input class="form-control" value="${item.approvedAt ? dtTime(item.approvedAt) : "-"}" readonly /></div>
            <div class="col-12"><label class="form-label">Approval Note</label><textarea class="form-control" readonly>${esc(item.approvalNote || "")}</textarea></div>
            <div class="col-12"><label class="form-label">Rejection Reason</label><textarea class="form-control" readonly>${esc(item.rejectionReason || "")}</textarea></div>
            <div class="col-12"><label class="form-label">Audit History</label><div class="audit-trail-card">${history.length ? history.map(h => `<div class="audit-item"><strong>${esc(h.actionType)}</strong><span>${esc((h.performedByUsername || 'System') + ' · ' + dtTime(h.createdAt))}</span><small>${esc(`${h.previousStatus || '-'} → ${h.newStatus || '-'}${h.note ? ` · ${h.note}` : ''}`)}</small></div>`).join('') : '<div class="empty-state">No leave audit history yet.</div>'}</div></div>
          </div>
        `, false);
      }));
    }

    [search, status, type].forEach(el => el?.addEventListener("input", () => { state.paging.leaves.page = 1; renderLeaves(); }));
    [status, type, pageSize].forEach(el => el?.addEventListener("change", () => { state.paging.leaves.page = 1; renderLeaves(); }));
    refresh?.addEventListener("click", load);
    load();
  }

  async function initDepartments() {
    const search = q("deptSearch");
    const pageSize = q("deptPageSize");
    const refresh = q("deptRefresh");
    const createDeptBtn = q("deptCreateBtn");
    const createDeptBtnInline = q("deptCreateBtnInline");
    const createPosBtn = q("positionCreateBtn");
    const createPosBtnInline = q("positionCreateBtnInline");
    const positionSearch = q("positionSearch");

    async function load() {
      try {
        clearError();
        const [departments, headcount, positions] = await Promise.all([
          api(endpoints.departments),
          api(endpoints.deptHeadcount).catch(() => []),
          api(endpoints.positions)
        ]);
        const headMap = new Map((headcount || []).map(x => [String(x.departmentCode || x.departmentId), x]));
        state.departments = (Array.isArray(departments) ? departments : []).map(d => {
          const matched = headMap.get(String(d.departmentCode)) || {};
          return {
            ...d,
            employeeCount: matched.employeeCount ?? 0,
            activeEmployeeCount: matched.activeEmployeeCount ?? 0,
            inactiveEmployeeCount: matched.inactiveEmployeeCount ?? 0
          };
        });
        state.positions = Array.isArray(positions) ? positions : [];
        renderDepartments();
        renderPositions();
      } catch (err) {
        showError(err.message || "Failed to load departments.");
      }
    }

    async function openDeptModal(item = null) {
      const submitBtn = await openFormModal({
        title: item ? "Edit Department" : "Create Department",
        bodyHtml: `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Department Code</label><input id="deptCodeInput" class="form-control" value="${esc(item?.departmentCode || "")}" /></div>
            <div class="col-md-6"><label class="form-label">Department Name</label><input id="deptNameInput" class="form-control" value="${esc(item?.departmentName || "")}" /></div>
            <div class="col-12"><div id="deptFormMessage" class="small text-danger"></div></div>
          </div>`,
        submitText: item ? "Save department" : "Create department"
      });
      submitBtn?.addEventListener("click", async () => {
        try {
          const payload = {
            departmentCode: q("deptCodeInput")?.value?.trim(),
            departmentName: q("deptNameInput")?.value?.trim()
          };
          if (item) await api(`/api/Departments/${item.id}`, "PUT", payload);
          else await api(endpoints.departments, "POST", payload);
          closeSharedModal();
          await load();
        } catch (err) {
          q("deptFormMessage").textContent = err.message || "Failed to save department.";
        }
      }, { once: true });
    }

    async function openPositionModal(item = null) {
      const submitBtn = await openFormModal({
        title: item ? "Edit Position" : "Create Position",
        bodyHtml: `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Position Code</label><input id="positionCodeInput" class="form-control" value="${esc(item?.positionCode || "")}" /></div>
            <div class="col-md-6"><label class="form-label">Position Name</label><input id="positionNameInput" class="form-control" value="${esc(item?.positionName || "")}" /></div>
            <div class="col-12"><div id="positionFormMessage" class="small text-danger"></div></div>
          </div>`,
        submitText: item ? "Save position" : "Create position"
      });
      submitBtn?.addEventListener("click", async () => {
        try {
          const payload = {
            positionCode: q("positionCodeInput")?.value?.trim(),
            positionName: q("positionNameInput")?.value?.trim()
          };
          if (item) await api(`/api/Positions/${item.id}`, "PUT", payload);
          else await api(endpoints.positions, "POST", payload);
          closeSharedModal();
          await load();
        } catch (err) {
          q("positionFormMessage").textContent = err.message || "Failed to save position.";
        }
      }, { once: true });
    }

    async function deleteDepartment(item) {
      if (!confirm(`Delete department ${item.departmentName}?`)) return;
      try {
        await api(`/api/Departments/${item.id}`, "DELETE");
        await load();
      } catch (err) {
        showError(err.message || "Failed to delete department.");
      }
    }

    async function deletePosition(item) {
      if (!confirm(`Delete position ${item.positionName}?`)) return;
      try {
        await api(`/api/Positions/${item.id}`, "DELETE");
        await load();
      } catch (err) {
        showError(err.message || "Failed to delete position.");
      }
    }

    function renderDepartments() {
      const term = (search?.value || "").trim().toLowerCase();
      state.paging.departments.pageSize = Number(pageSize?.value || 10);
      let items = [...state.departments];
      if (term) {
        items = items.filter(x =>
          [x.departmentCode, x.departmentName]
            .filter(Boolean).join(" ").toLowerCase().includes(term)
        );
      }
      const pageItems = slicePaged(items, "departments");
      const body = q("deptBody");
      body.innerHTML = pageItems.length ? pageItems.map(x => `
        <tr>
          <td><strong>${esc(x.departmentCode)}</strong></td>
          <td>${esc(x.departmentName)}</td>
          <td>${x.employeeCount ?? 0}</td>
          <td>${x.activeEmployeeCount ?? 0}</td>
          <td>${x.inactiveEmployeeCount ?? 0}</td>
          <td>
            <div class="action-group">
              <button class="btn btn-sm btn-outline-primary" data-view-dept-id="${x.id}">View</button>
              <button class="btn btn-sm btn-outline-secondary" data-edit-dept-id="${x.id}">Edit</button>
              ${state.role === "Admin" ? `<button class="btn btn-sm btn-outline-danger" data-delete-dept-id="${x.id}">Delete</button>` : ""}
            </div>
          </td>
        </tr>`).join("") :
        `<tr><td colspan="6"><div class="empty-state">No departments match the current filters.</div></td></tr>`;
      renderPaging("deptPaging", "departments", items.length, renderDepartments);
      setStatGrid("deptQuickStats", [
        ["Departments", items.length],
        ["Employees", items.reduce((a,b)=>a+(Number(b.employeeCount)||0),0)],
        ["Active", items.reduce((a,b)=>a+(Number(b.activeEmployeeCount)||0),0)],
        ["Positions", state.positions.length]
      ]);

      body.querySelectorAll("[data-view-dept-id]").forEach(btn => btn.addEventListener("click", async () => {
        const detail = await api(`/api/Departments/${btn.dataset.viewDeptId}`);
        const employeeRows = (detail.employees || []).length
          ? `<ul class="mb-0">${detail.employees.slice(0, 8).map(emp => `<li>${esc(emp.employeeCode)} - ${esc(emp.fullName)}</li>`).join("")}</ul>`
          : `<span class="text-muted">No employees in this department.</span>`;
        await openDetailModal("Department Details", `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Department Code</label><input class="form-control" value="${esc(detail.departmentCode)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Department Name</label><input class="form-control" value="${esc(detail.departmentName)}" readonly /></div>
            <div class="col-12"><label class="form-label">Sample Employees</label><div class="border rounded-4 p-3 bg-light-subtle">${employeeRows}</div></div>
          </div>`, false);
      }));
      body.querySelectorAll("[data-edit-dept-id]").forEach(btn => btn.addEventListener("click", () => {
        const item = state.departments.find(x => String(x.id) === String(btn.dataset.editDeptId));
        if (item) openDeptModal(item);
      }));
      body.querySelectorAll("[data-delete-dept-id]").forEach(btn => btn.addEventListener("click", () => {
        const item = state.departments.find(x => String(x.id) === String(btn.dataset.deleteDeptId));
        if (item) deleteDepartment(item);
      }));
    }

    function renderPositions() {
      const term = (positionSearch?.value || "").trim().toLowerCase();
      const items = term
        ? state.positions.filter(x => [x.positionCode, x.positionName].filter(Boolean).join(" ").toLowerCase().includes(term))
        : [...state.positions];
      const body = q("positionBody");
      if (!body) return;
      body.innerHTML = items.length ? items.map(x => `
        <tr>
          <td><strong>${esc(x.positionCode)}</strong></td>
          <td>${esc(x.positionName)}</td>
          <td>${x.employeeCount ?? 0}</td>
          <td>
            <div class="action-group">
              <button class="btn btn-sm btn-outline-primary" data-view-position-id="${x.id}">View</button>
              <button class="btn btn-sm btn-outline-secondary" data-edit-position-id="${x.id}">Edit</button>
              ${state.role === "Admin" ? `<button class="btn btn-sm btn-outline-danger" data-delete-position-id="${x.id}">Delete</button>` : ""}
            </div>
          </td>
        </tr>`).join("") : `<tr><td colspan="4"><div class="empty-state">No positions found.</div></td></tr>`;
      body.querySelectorAll("[data-view-position-id]").forEach(btn => btn.addEventListener("click", async () => {
        const detail = await api(`/api/Positions/${btn.dataset.viewPositionId}`);
        const employeeRows = (detail.employees || []).length
          ? `<ul class="mb-0">${detail.employees.slice(0, 8).map(emp => `<li>${esc(emp.employeeCode)} - ${esc(emp.fullName)}</li>`).join("")}</ul>`
          : `<span class="text-muted">No employees linked to this position.</span>`;
        await openDetailModal("Position Details", `
          <div class="row g-3">
            <div class="col-md-6"><label class="form-label">Position Code</label><input class="form-control" value="${esc(detail.positionCode)}" readonly /></div>
            <div class="col-md-6"><label class="form-label">Position Name</label><input class="form-control" value="${esc(detail.positionName)}" readonly /></div>
            <div class="col-12"><label class="form-label">Sample Employees</label><div class="border rounded-4 p-3 bg-light-subtle">${employeeRows}</div></div>
          </div>`, false);
      }));
      body.querySelectorAll("[data-edit-position-id]").forEach(btn => btn.addEventListener("click", () => {
        const item = state.positions.find(x => String(x.id) === String(btn.dataset.editPositionId));
        if (item) openPositionModal(item);
      }));
      body.querySelectorAll("[data-delete-position-id]").forEach(btn => btn.addEventListener("click", () => {
        const item = state.positions.find(x => String(x.id) === String(btn.dataset.deletePositionId));
        if (item) deletePosition(item);
      }));
    }

    search?.addEventListener("input", () => { state.paging.departments.page = 1; renderDepartments(); });
    pageSize?.addEventListener("change", () => { state.paging.departments.page = 1; renderDepartments(); });
    positionSearch?.addEventListener("input", renderPositions);
    refresh?.addEventListener("click", load);
    createDeptBtn?.addEventListener("click", () => openDeptModal());
    createDeptBtnInline?.addEventListener("click", () => openDeptModal());
    createPosBtn?.addEventListener("click", () => openPositionModal());
    createPosBtnInline?.addEventListener("click", () => openPositionModal());
    load();
  }


  async function initReports() {
    const year = q("repYear");
    const month = q("repMonth");
    const refresh = q("repRefresh");
    const exportSummaryBtn = q("repExportSummary");
    const exportPayrollBtn = q("repExportPayrolls");
    const exportLeaveBtn = q("repExportLeaves");
    const exportAdjustmentsBtn = q("repExportAdjustments");
    const exportAdjustmentAuditBtn = q("repExportAdjustmentAudit");
    const exportLeaveAuditBtn = q("repExportLeaveAudit");
    const exportPayrollAuditBtn = q("repExportPayrollAudit");
    const exportFormat = q("repExportFormat");
    const refreshExportsBtn = q("repRefreshExports");
    const exportMessage = q("reportExportMessage");
    const drillScope = q("repDrillScope");
    const drillStatus = q("repDrillStatus");
    const drillSearch = q("repDrillSearch");
    const drillReset = q("repDrillReset");
    const trendMetric = q("repTrendMetric");
    const trendReset = q("repTrendReset");
    const aiTemplate = q("repAiTemplate");
    const aiDepartment = q("repAiDepartment");
    const aiPrompt = q("repAiPrompt");
    const aiRunBtn = q("repAiRun");
    const aiUseScriptBtn = q("repAiUseScript");
    const aiMessage = q("repAiMessage");
    const aiResult = q("repAiResult");
    const reportScriptBody = q("reportScriptBody");
    const now = new Date();
    const reportState = { data: null, drill: { scope: 'queue', status: '', search: '', context: 'Open approval queue for the selected month.' }, trendMetric: 'totalNetSalary' };
    if (year) year.value = now.getFullYear();
    if (month) month.value = now.getMonth() + 1;

    async function setupAiPanel() {
      if (!aiTemplate || !aiDepartment || !aiPrompt || !aiMessage || !aiResult) return;
      try {
        await ensureDepartmentsLoaded();
        await ensureAiTemplatesLoaded();
        populateDepartmentSelect('repAiDepartment', 'All departments');
        populateAiTemplateControls('repAiTemplate', 'repAiTemplateHelper', 'repAiPrompt', 'executive-brief');
        if (state.role === 'Manager' && aiDepartment) {
          aiDepartment.disabled = true;
          aiDepartment.value = '';
        }
        aiMessage.className = 'small text-muted';
        aiMessage.textContent = 'Cached AI summaries are reused for identical report scopes to keep the assistant fast.';
      } catch (err) {
        if (aiMessage) {
          aiMessage.className = 'small text-danger';
          aiMessage.textContent = err.message || 'Failed to load AI assistant controls.';
        }
      }
    }

    async function triggerAiSummary(usePresentationContext = false) {
      if (!aiTemplate || !aiPrompt) return;
      const selectedDepartment = aiDepartment?.value || '';
      let customPrompt = aiPrompt.value?.trim() || '';
      if (usePresentationContext && !customPrompt) {
        const scriptText = (reportScriptBody?.textContent || '').trim().replace(/\s+/g, ' ');
        customPrompt = `Summarize payroll anomalies, workforce pressure, and attendance risk for ${String(month?.value || '').padStart(2, '0')}/${year?.value || ''}. ${scriptText}`.trim();
        aiPrompt.value = customPrompt;
      }
      await runAiPayrollSummary({
        month: month?.value || (now.getMonth() + 1),
        year: year?.value || now.getFullYear(),
        departmentId: selectedDepartment,
        templateKey: aiTemplate.value || 'executive-brief',
        prompt: customPrompt,
        messageId: 'repAiMessage',
        resultId: 'repAiResult',
        scopeLabel: 'Reports assistant'
      });
    }

    function renderReportChart(key, canvasId, config) {
      const el = q(canvasId);
      if (!el || typeof Chart === 'undefined') return;
      state.charts[key]?.destroy?.();
      state.charts[key] = new Chart(el, config);
    }

    function monthRangeStrings(yearValue, monthValue) {
      const yNum = Number(yearValue);
      const mNum = Number(monthValue);
      const lastDay = new Date(yNum, mNum, 0).getDate();
      const mm = String(mNum).padStart(2, '0');
      return {
        fromDate: `${yNum}-${mm}-01`,
        toDate: `${yNum}-${mm}-${String(lastDay).padStart(2, '0')}`
      };
    }

    function setDrillStatusOptions(scope) {
      if (!drillStatus) return;
      const options = {
        queue: [['', 'All pending items'], ['Pending', 'Pending']],
        attendance: [['', 'All statuses'], ['Present', 'Present'], ['Late', 'Late'], ['Absent', 'Absent'], ['Leave', 'Leave'], ['Remote', 'Remote']],
        leave: [['', 'All statuses'], ['Pending', 'Pending'], ['Approved', 'Approved'], ['Rejected', 'Rejected'], ['Cancelled', 'Cancelled']],
        adjustment: [['', 'All statuses'], ['Pending', 'Pending'], ['Approved', 'Approved'], ['Rejected', 'Rejected']],
        payroll: [['', 'All payrolls']],
        employee: [['', 'All employees'], ['Active', 'Active'], ['Inactive', 'Inactive']]
      }[scope] || [['', 'All']];
      drillStatus.innerHTML = options.map(([value, label]) => `<option value="${esc(value)}">${esc(label)}</option>`).join('');
      if (![...drillStatus.options].some(o => o.value === reportState.drill.status)) {
        reportState.drill.status = '';
      }
      drillStatus.value = reportState.drill.status;
    }

    function setDrillInsights(items) {
      const host = q('repDrillInsightCards');
      if (!host) return;
      host.innerHTML = (items || []).map(item => `
        <div class="role-kpi-card">
          <div class="mini-badge"><i class="bi ${esc(item.icon || 'bi-filter-circle')}"></i><span>${esc(item.badge || 'Insight')}</span></div>
          <span>${esc(item.label)}</span>
          <strong>${esc(item.value)}</strong>
          <small>${esc(item.note || '-')}</small>
        </div>`).join('');
    }

    function textHaystack(parts) {
      return parts.filter(Boolean).join(' ').toLowerCase();
    }

    function renderDrillTable(columns, rows, emptyText) {
      const head = q('repDrillHead');
      const body = q('repDrillBody');
      if (!head || !body) return;
      head.innerHTML = columns.map(col => `<th>${esc(col)}</th>`).join('');
      body.innerHTML = rows.length ? rows.map(row => `<tr>${row.map(cell => `<td>${cell}</td>`).join('')}</tr>`).join('') : `<tr><td colspan="${columns.length}"><div class="empty-state">${esc(emptyText)}</div></td></tr>`;
    }

    function renderDrillDown() {
      const data = reportState.data;
      if (!data) return;
      const scope = reportState.drill.scope || 'queue';
      const status = String(reportState.drill.status || '').toLowerCase();
      const search = String(reportState.drill.search || '').trim().toLowerCase();
      const contextHost = q('repDrillContext');
      const subtitle = q('repDrillSubtitle');
      if (contextHost) contextHost.innerHTML = `<strong>Current lens:</strong> ${esc(reportState.drill.context || 'Interactive drill-down is ready.')}`;

      if (scope === 'attendance') {
        const filtered = (data.attendanceRecords || []).filter(item => {
          const matchesStatus = !status || String(item.status || '').toLowerCase() === status;
          const hay = textHaystack([item.fullName, item.departmentName, item.positionName, item.note, item.status]);
          return matchesStatus && (!search || hay.includes(search));
        });
        if (subtitle) subtitle.textContent = 'Attendance records filtered by chart selection and controls.';
        setDrillInsights([
          { badge: 'Records', icon: 'bi-calendar-check', label: 'Matching attendance records', value: filtered.length, note: 'Rows currently visible in the drill-down table.' },
          { badge: 'Quality', icon: 'bi-exclamation-circle', label: 'Late or absent', value: filtered.filter(x => ['late','absent'].includes(String(x.status || '').toLowerCase())).length, note: 'Attendance records that may require attention.' },
          { badge: 'Note', icon: 'bi-chat-square-text', label: 'With note', value: filtered.filter(x => (x.note || '').trim()).length, note: 'Records that include a note or context for review.' }
        ]);
        renderDrillTable(['Employee', 'Date', 'Status', 'Department', 'Time Window', 'Note'], filtered.slice(0, 40).map(item => [
          esc(item.fullName || '-'),
          esc(dt(item.workDate)),
          `<span class="badge-soft ${statusBadgeClass(item.status)}">${esc(item.status)}</span>`,
          esc(item.departmentName || '-'),
          esc(`${item.checkInTime ? dtTime(item.checkInTime) : '-'} → ${item.checkOutTime ? dtTime(item.checkOutTime) : '-'}`),
          esc(item.note || '-')
        ]), 'No attendance records match the current drill-down selection.');
        return;
      }

      if (scope === 'leave') {
        const filtered = (data.leaveRequests || []).filter(item => {
          const matchesStatus = !status || String(item.status || '').toLowerCase() === status;
          const hay = textHaystack([item.fullName, item.leaveType, item.reason, item.approvedByUsername, item.status]);
          return matchesStatus && (!search || hay.includes(search));
        });
        if (subtitle) subtitle.textContent = 'Leave workflow records filtered by selected status and search keywords.';
        setDrillInsights([
          { badge: 'Workflow', icon: 'bi-journal-check', label: 'Matching leave requests', value: filtered.length, note: 'Filtered leave workflow items currently visible.' },
          { badge: 'Approved', icon: 'bi-check2-circle', label: 'Approved leave days', value: filtered.filter(x => String(x.status || '').toLowerCase() === 'approved').reduce((sum, item) => sum + Number(item.totalDays || 0), 0), note: 'Total approved leave days inside the filtered set.' },
          { badge: 'Pending', icon: 'bi-hourglass-split', label: 'Pending decisions', value: filtered.filter(x => String(x.status || '').toLowerCase() === 'pending').length, note: 'Leave requests still waiting for approval.' }
        ]);
        renderDrillTable(['Employee', 'Type', 'Dates', 'Days', 'Status', 'Reviewer / Reason'], filtered.slice(0, 40).map(item => [
          esc(item.fullName || '-'),
          esc(item.leaveType || '-'),
          esc(`${dt(item.startDate)} → ${dt(item.endDate)}`),
          esc(item.totalDays ?? '-'),
          `<span class="badge-soft ${statusBadgeClass(item.status)}">${esc(item.status)}</span>`,
          esc(`${item.approvedByUsername || '-'}${item.rejectionReason ? ` · ${item.rejectionReason}` : item.approvalNote ? ` · ${item.approvalNote}` : ''}`)
        ]), 'No leave requests match the current drill-down selection.');
        return;
      }

      if (scope === 'adjustment') {
        const filtered = (data.adjustmentRequests || []).filter(item => {
          const matchesStatus = !status || String(item.status || '').toLowerCase() === status;
          const hay = textHaystack([item.fullName, item.departmentName, item.requestedStatus, item.reason, item.reviewedByUsername, item.reviewNote, item.status]);
          return matchesStatus && (!search || hay.includes(search));
        });
        if (subtitle) subtitle.textContent = 'Attendance-adjustment requests filtered by workflow state and search keywords.';
        setDrillInsights([
          { badge: 'Requests', icon: 'bi-arrow-repeat', label: 'Matching adjustment requests', value: filtered.length, note: 'Adjustment requests now visible in the drill-down table.' },
          { badge: 'Pending', icon: 'bi-hourglass', label: 'Pending review', value: filtered.filter(x => String(x.status || '').toLowerCase() === 'pending').length, note: 'Requests still waiting for HR/Admin response.' },
          { badge: 'Reviewed', icon: 'bi-check2-square', label: 'Reviewed requests', value: filtered.filter(x => String(x.status || '').toLowerCase() !== 'pending').length, note: 'Requests that already completed the review workflow.' }
        ]);
        renderDrillTable(['Employee', 'Work Date', 'Requested Status', 'Status', 'Reviewer', 'Reason / Review'], filtered.slice(0, 40).map(item => [
          esc(item.fullName || '-'),
          esc(dt(item.workDate)),
          esc(item.requestedStatus || '-'),
          `<span class="badge-soft ${statusBadgeClass(item.status)}">${esc(item.status)}</span>`,
          esc(item.reviewedByUsername || '-'),
          esc(`${item.reason || '-'}${item.reviewNote ? ` · ${item.reviewNote}` : ''}`)
        ]), 'No adjustment requests match the current drill-down selection.');
        return;
      }

      if (scope === 'payroll') {
        const filtered = (data.payrollRecords || []).filter(item => {
          const hay = textHaystack([item.fullName, item.departmentName, item.positionName, item.employeeCode, item.payrollMonth, item.payrollYear]);
          return !search || hay.includes(search);
        });
        if (subtitle) subtitle.textContent = 'Payroll records filtered by employee search keywords.';
        const totalNet = filtered.reduce((sum, item) => sum + Number(item.netSalary || 0), 0);
        setDrillInsights([
          { badge: 'Payroll', icon: 'bi-wallet2', label: 'Matching payroll records', value: filtered.length, note: 'Payroll rows currently included in the drill-down table.' },
          { badge: 'Net', icon: 'bi-cash-stack', label: 'Total net salary', value: money(totalNet), note: 'Combined net salary value for the filtered payroll rows.' },
          { badge: 'Average', icon: 'bi-graph-up-arrow', label: 'Average net salary', value: money(filtered.length ? totalNet / filtered.length : 0), note: 'Average net salary for the filtered payroll set.' }
        ]);
        renderDrillTable(['Employee', 'Department', 'Month', 'Net Salary', 'Working Days', 'Generated'], filtered.slice(0, 40).map(item => [
          esc(item.fullName || '-'),
          esc(item.departmentName || '-'),
          esc(`${item.payrollMonth}/${item.payrollYear}`),
          esc(money(item.netSalary)),
          esc(`${item.effectiveWorkingDays ?? '-'} effective · ${item.leaveDays ?? 0} leave`),
          esc(dtTime(item.generatedAt))
        ]), 'No payroll records match the current drill-down selection.');
        return;
      }

      if (scope === 'employee') {
        const filtered = (data.employees || []).filter(item => {
          const statusMatch = !status || (status === 'active' ? !!item.isActive : !item.isActive);
          const hay = textHaystack([item.employeeCode, item.fullName, item.departmentName, item.positionName, item.username, item.accountRole, item.email]);
          return statusMatch && (!search || hay.includes(search));
        });
        if (subtitle) subtitle.textContent = 'Employee roster filtered by active state and search keywords.';
        setDrillInsights([
          { badge: 'Workforce', icon: 'bi-people', label: 'Matching employees', value: filtered.length, note: 'Employee records visible for the current drill-down lens.' },
          { badge: 'Account', icon: 'bi-person-badge', label: 'With login account', value: filtered.filter(x => x.hasLoginAccount).length, note: 'Employees that already have linked application accounts.' },
          { badge: 'Inactive', icon: 'bi-person-dash', label: 'Inactive in result', value: filtered.filter(x => !x.isActive).length, note: 'Inactive employees present in the current filtered roster.' }
        ]);
        renderDrillTable(['Code', 'Employee', 'Department', 'Position', 'Status', 'Account'], filtered.slice(0, 40).map(item => [
          esc(item.employeeCode || '-'),
          esc(item.fullName || '-'),
          esc(item.departmentName || '-'),
          esc(item.positionName || '-'),
          `<span class="badge-soft ${statusBadgeClass(item.isActive ? 'Active' : 'Inactive')}">${esc(item.isActive ? 'Active' : 'Inactive')}</span>`,
          esc(item.hasLoginAccount ? `${item.username || '-'} (${item.accountRole || '-'})` : 'No account')
        ]), 'No employees match the current drill-down selection.');
        return;
      }

      const pendingLeaveRows = (data.leaveRequests || []).filter(item => String(item.status || '').toLowerCase() === 'pending').map(item => ({
        type: 'Leave request',
        owner: item.fullName,
        status: item.status,
        createdAt: item.createdAt,
        meta: `${item.leaveType} · ${dt(item.startDate)} → ${dt(item.endDate)}`
      }));
      const adjustmentQueueRows = (data.adjustmentRequests || []).filter(item => String(item.status || '').toLowerCase() === 'pending').map(item => ({
        type: 'Attendance adjustment',
        owner: item.fullName,
        status: item.status,
        createdAt: item.createdAt,
        meta: `${item.requestedStatus} · ${dt(item.workDate)}`
      }));
      const filtered = [...adjustmentQueueRows, ...pendingLeaveRows].filter(item => {
        const hay = textHaystack([item.type, item.owner, item.meta, item.status]);
        return (!status || String(item.status || '').toLowerCase() === status) && (!search || hay.includes(search));
      }).sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0));
      if (subtitle) subtitle.textContent = 'Open approval queue, combining pending leave requests and attendance adjustments.';
      setDrillInsights([
        { badge: 'Queue', icon: 'bi-hourglass-split', label: 'Pending items', value: filtered.length, note: 'Queue items currently visible in the drill-down table.' },
        { badge: 'Leave', icon: 'bi-journal-arrow-up', label: 'Pending leaves', value: pendingLeaveRows.length, note: 'Leave requests still awaiting approval.' },
        { badge: 'Adjustment', icon: 'bi-arrow-repeat', label: 'Pending adjustments', value: adjustmentQueueRows.length, note: 'Attendance corrections waiting for review.' }
      ]);
      renderDrillTable(['Type', 'Owner', 'Details', 'Status', 'Created'], filtered.slice(0, 40).map(item => [
        esc(item.type),
        esc(item.owner || '-'),
        esc(item.meta || '-'),
        `<span class="badge-soft ${statusBadgeClass(item.status)}">${esc(item.status)}</span>`,
        esc(dtTime(item.createdAt))
      ]), 'No queue items match the current drill-down selection.');
    }

    function applyDrill(scope, statusValue = '', contextText = '') {
      reportState.drill.scope = scope;
      reportState.drill.status = statusValue || '';
      reportState.drill.context = contextText || 'Interactive drill-down was updated from the selected chart.';
      if (drillScope) drillScope.value = scope;
      setDrillStatusOptions(scope);
      if (drillStatus) drillStatus.value = reportState.drill.status;
      renderDrillDown();
    }

    function drawCharts(overview, attendance, payroll, leave, pendingAdjustments) {
      renderReportChart('reportsAttendance', 'repAttendanceChart', {
        type: 'doughnut',
        data: {
          labels: ['Present', 'Late', 'Absent', 'Leave', 'Remote'],
          datasets: [{
            data: [attendance.presentCount ?? 0, attendance.lateCount ?? 0, attendance.absentCount ?? 0, attendance.leaveCount ?? 0, attendance.remoteCount ?? 0],
            backgroundColor: ['rgba(37,99,235,.82)','rgba(245,158,11,.82)','rgba(239,68,68,.82)','rgba(139,92,246,.82)','rgba(16,185,129,.82)'],
            borderWidth: 0
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { position: 'bottom' } },
          onClick: (_, elements, chart) => {
            if (!elements.length) return;
            const label = chart.data.labels?.[elements[0].index];
            applyDrill('attendance', label, `Attendance chart selected: ${label}. The detail table now shows matching attendance records for ${month.value}/${year.value}.`);
          }
        }
      });

      renderReportChart('reportsLeave', 'repLeaveChart', {
        type: 'bar',
        data: {
          labels: ['Pending', 'Approved', 'Rejected', 'Cancelled'],
          datasets: [{
            label: 'Leave requests',
            data: [leave.pendingRequests ?? 0, leave.approvedRequests ?? 0, leave.rejectedRequests ?? 0, leave.cancelledRequests ?? 0],
            backgroundColor: ['rgba(245,158,11,.82)','rgba(16,185,129,.82)','rgba(239,68,68,.82)','rgba(100,116,139,.82)'],
            borderRadius: 12,
            maxBarThickness: 48
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
          onClick: (_, elements, chart) => {
            if (!elements.length) return;
            const label = chart.data.labels?.[elements[0].index];
            applyDrill('leave', label, `Leave workflow chart selected: ${label}. The detail table now highlights matching leave requests in the reporting period.`);
          }
        }
      });

      renderReportChart('reportsWorkforce', 'repWorkforceChart', {
        type: 'bar',
        data: {
          labels: ['Active employees', 'Inactive employees', 'Payroll records', 'Pending approvals'],
          datasets: [{
            label: 'Operational coverage',
            data: [overview.activeEmployees ?? 0, overview.inactiveEmployees ?? 0, payroll.totalPayrollRecords ?? 0, (leave.pendingRequests ?? 0) + (pendingAdjustments.length || 0)],
            backgroundColor: ['rgba(16,185,129,.82)','rgba(100,116,139,.82)','rgba(37,99,235,.82)','rgba(245,158,11,.82)'],
            borderRadius: 12,
            maxBarThickness: 46
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: { y: { beginAtZero: true } },
          onClick: (_, elements, chart) => {
            if (!elements.length) return;
            const label = chart.data.labels?.[elements[0].index];
            if (label === 'Active employees') return applyDrill('employee', 'Active', `Workforce chart selected: ${label}. The detail table now lists active employees in the current system snapshot.`);
            if (label === 'Inactive employees') return applyDrill('employee', 'Inactive', `Workforce chart selected: ${label}. The detail table now lists inactive employee records for cleanup or access review.`);
            if (label === 'Payroll records') return applyDrill('payroll', '', `Workforce chart selected: ${label}. The detail table now lists payroll records for ${month.value}/${year.value}.`);
            return applyDrill('queue', 'Pending', `Workforce chart selected: ${label}. The detail table now shows pending approval items in the selected month.`);
          }
        }
      });

    }

    function renderDepartmentComparison(items) {
      const body = q('repDepartmentBody');
      const rows = Array.isArray(items) ? [...items].sort((a, b) => Number(b.totalNetSalary || 0) - Number(a.totalNetSalary || 0)) : [];
      if (body) {
        body.innerHTML = rows.length ? rows.map((item, index) => `
          <tr>
            <td>
              <div class="d-flex flex-column gap-1">
                <span class="department-rank-chip"><i class="bi bi-diagram-3"></i> #${index + 1}</span>
                <strong>${esc(item.departmentName || '-')}</strong>
                <small class="text-muted">${esc(item.departmentCode || '-')}</small>
              </div>
            </td>
            <td>${item.headcount ?? 0}</td>
            <td>${item.activeEmployees ?? 0}</td>
            <td>${item.attendanceRecords ?? 0}</td>
            <td>${item.approvedLeaveCount ?? 0}</td>
            <td>${(item.pendingLeaveCount ?? 0) + (item.pendingAdjustmentCount ?? 0)}</td>
            <td>${item.payrollCoverage ?? 0}</td>
            <td>${money(item.totalNetSalary)}</td>
          </tr>`).join('') : `<tr><td colspan="8"><div class="empty-state">No department comparison data is available for the selected month.</div></td></tr>`;
      }
      renderReportChart('reportsDepartmentComparison', 'repDepartmentComparisonChart', {
        type: 'bar',
        data: {
          labels: rows.map(item => item.departmentName || '-'),
          datasets: [
            { label: 'Headcount', data: rows.map(item => item.headcount ?? 0), backgroundColor: 'rgba(37,99,235,.82)', borderRadius: 10, maxBarThickness: 34 },
            { label: 'Pending actions', data: rows.map(item => (item.pendingLeaveCount ?? 0) + (item.pendingAdjustmentCount ?? 0)), backgroundColor: 'rgba(245,158,11,.82)', borderRadius: 10, maxBarThickness: 34 },
            { label: 'Payroll coverage', data: rows.map(item => item.payrollCoverage ?? 0), backgroundColor: 'rgba(16,185,129,.82)', borderRadius: 10, maxBarThickness: 34 }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { position: 'bottom' } },
          scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
          onClick: (_, elements, chart) => {
            if (!elements.length) return;
            const label = chart.data.labels?.[elements[0].index];
            if (!label) return;
            reportState.drill.scope = 'employee';
            reportState.drill.status = '';
            reportState.drill.search = label;
            reportState.drill.context = `Department comparison selected: ${label}. The drill-down table now searches employees and records related to that department.`;
            if (drillScope) drillScope.value = 'employee';
            if (drillSearch) drillSearch.value = label;
            setDrillStatusOptions('employee');
            renderDrillDown();
          }
        }
      });
    }

    function renderTrendCharts(items) {
      const rows = Array.isArray(items) ? items : [];
      const metric = reportState.trendMetric || 'totalNetSalary';
      const metricLabelMap = {
        totalNetSalary: 'Total Net Salary',
        attendanceRecords: 'Attendance Records',
        approvedLeaves: 'Approved Leaves',
        pendingAdjustments: 'Pending Adjustments',
        averageNetSalary: 'Average Net Salary'
      };
      renderReportChart('reportsTrend', 'repTrendChart', {
        type: metric === 'totalNetSalary' || metric === 'averageNetSalary' ? 'line' : 'bar',
        data: {
          labels: rows.map(item => item.periodLabel || '-'),
          datasets: [{
            label: metricLabelMap[metric] || 'Trend',
            data: rows.map(item => Number(item[metric] || 0)),
            backgroundColor: 'rgba(37,99,235,.24)',
            borderColor: 'rgba(37,99,235,.95)',
            fill: metric === 'totalNetSalary' || metric === 'averageNetSalary',
            tension: 0.32,
            borderWidth: 3,
            pointRadius: 4,
            pointHoverRadius: 5,
            borderRadius: 10,
            maxBarThickness: 40
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: { y: { beginAtZero: true } },
          onClick: async (_, elements) => {
            if (!elements.length) return;
            const selected = rows[elements[0].index];
            if (!selected) return;
            if (year) year.value = selected.year;
            if (month) month.value = selected.month;
            reportState.drill.scope = metric === 'approvedLeaves' ? 'leave' : metric === 'pendingAdjustments' ? 'adjustment' : metric === 'attendanceRecords' ? 'attendance' : 'payroll';
            reportState.drill.status = metric === 'approvedLeaves' ? 'Approved' : metric === 'pendingAdjustments' ? 'Pending' : '';
            reportState.drill.search = '';
            reportState.drill.context = `Monthly trend selected: ${selected.periodLabel}. The report workspace has been refreshed to that month.`;
            if (drillScope) drillScope.value = reportState.drill.scope;
            if (drillSearch) drillSearch.value = '';
            setDrillStatusOptions(reportState.drill.scope);
            if (drillStatus) drillStatus.value = reportState.drill.status;
            load();
          }
        }
      });
    }

    async function load() {
      try {
        clearError();
        const { fromDate, toDate } = monthRangeStrings(year.value, month.value);
        const [overview, attendance, payroll, leave, recentAttendance, recentLeave, pendingAdjustments, adjustmentAudit, leaveAudit, payrollAudit, employees, attendanceRecords, leaveRequests, payrollRecords, adjustmentRequests, departmentComparison, monthlyTrends] = await Promise.all([
          api(endpoints.overview(year.value, month.value)),
          api(endpoints.attendanceMonthly(year.value, month.value)),
          api(endpoints.payrollMonthly(year.value, month.value)),
          api(endpoints.leaveMonthly(year.value, month.value)),
          api(endpoints.recentAttendance(6)),
          api(endpoints.recentLeave(6)),
          api(`/api/Attendances/adjustment-requests?${buildQuery({ status: "Pending", month: month.value, year: year.value })}`),
          api('/api/Attendances/adjustment-history/recent?take=6').catch(() => []),
          api('/api/LeaveRequests/history/recent?take=6').catch(() => []),
          api('/api/Payrolls/history/recent?take=6').catch(() => []),
          api(endpoints.employees).catch(() => []),
          api(endpoints.attendances(buildQuery({ month: month.value, year: year.value }))).catch(() => []),
          api(`${endpoints.leaves}?${buildQuery({ fromDate, toDate })}`).catch(() => []),
          api(endpoints.payrolls(buildQuery({ month: month.value, year: year.value }))).catch(() => []),
          api(`/api/Attendances/adjustment-requests?${buildQuery({ month: month.value, year: year.value })}`).catch(() => []),
          api(`/api/Reports/department-comparison?${buildQuery({ year: year.value, month: month.value })}`).catch(() => []),
          api(`/api/Reports/monthly-trends?${buildQuery({ year: year.value, month: month.value, monthsBack: 6 })}`).catch(() => [])
        ]);

        reportState.data = { overview, attendance, payroll, leave, recentAttendance, recentLeave, pendingAdjustments: pendingAdjustments || [], adjustmentAudit, leaveAudit, payrollAudit, employees: employees || [], attendanceRecords: attendanceRecords || [], leaveRequests: leaveRequests || [], payrollRecords: payrollRecords || [], adjustmentRequests: adjustmentRequests || [], departmentComparison: departmentComparison || [], monthlyTrends: monthlyTrends || [] };

        setStatGrid("repOverviewStats", [["Total Employees", overview.totalEmployees ?? 0], ["Active Employees", overview.activeEmployees ?? 0], ["Pending Leaves", overview.pendingLeaveRequests ?? 0], ["Monthly Net Salary", money(overview.monthlyTotalNetSalary)]]);
        setStatGrid("repAttendanceStats", [["Present", attendance.presentCount ?? 0], ["Late", attendance.lateCount ?? 0], ["Absent", attendance.absentCount ?? 0], ["Leave", attendance.leaveCount ?? 0], ["Remote", attendance.remoteCount ?? 0]]);
        setStatGrid("repPayrollStats", [["Payroll Records", payroll.totalPayrollRecords ?? 0], ["Total Bonus", money(payroll.totalBonus)], ["Total Deduction", money(payroll.totalDeduction)], ["Total Net Salary", money(payroll.totalNetSalary)], ["Average Net Salary", money(payroll.averageNetSalary)]]);
        setStatGrid("repLeaveStats", [["Total Requests", leave.totalRequests ?? 0], ["Pending", leave.pendingRequests ?? 0], ["Approved", leave.approvedRequests ?? 0], ["Rejected", leave.rejectedRequests ?? 0], ["Cancelled", leave.cancelledRequests ?? 0]]);
        drawCharts(overview, attendance, payroll, leave, pendingAdjustments || []);
        renderDepartmentComparison(departmentComparison || []);
        renderTrendCharts(monthlyTrends || []);

        const highlightHost = q("repOperationalHighlights");
        if (highlightHost) {
          const highlights = [
            `${attendance.presentCount ?? 0} present records were processed for ${month.value}/${year.value}.`,
            `${pendingAdjustments.length || 0} attendance adjustments are still waiting for review.`,
            `${leave.pendingRequests ?? 0} leave requests are pending decision in the current reporting period.`,
            `${payroll.totalPayrollRecords ?? 0} payroll records were generated with average net salary ${money(payroll.averageNetSalary)}.`
          ];
          highlightHost.innerHTML = highlights.map(item => `<div class="inline-metric-item"><i class="bi bi-check2-circle"></i><span>${esc(item)}</span></div>`).join("");
        }

        const queueBody = q("repQueueBody");
        if (queueBody) {
          const rows = [
            ...(Array.isArray(pendingAdjustments) ? pendingAdjustments.slice(0, 4).map(x => ({ type: "Attendance adjustment", owner: x.fullName, status: x.status, time: dtTime(x.createdAt), meta: `${x.requestedStatus} · ${dt(x.workDate)}` })) : []),
            ...(Array.isArray(recentLeave) ? recentLeave.filter(x => String(x.status).toLowerCase() === "pending").slice(0, 4).map(x => ({ type: "Leave request", owner: x.fullName, status: x.status, time: dtTime(x.createdAt), meta: `${x.leaveType} · ${dt(x.startDate)} → ${dt(x.endDate)}` })) : [])
          ].slice(0, 8);
          queueBody.innerHTML = rows.length ? rows.map(row => `
            <tr>
              <td>${esc(row.type)}</td>
              <td>${esc(row.owner || "-")}</td>
              <td>${esc(row.meta || "-")}</td>
              <td><span class="badge-soft ${statusBadgeClass(row.status)}">${esc(row.status)}</span></td>
              <td>${esc(row.time)}</td>
            </tr>`).join("") : `<tr><td colspan="5"><div class="empty-state">No pending approval items for the selected snapshot.</div></td></tr>`;
        }

        const timelineItems = [
          ...(Array.isArray(recentAttendance) ? recentAttendance.slice(0, 2).map(x => ({ title: `Attendance: ${x.fullName}`, subtitle: `${x.status} on ${dt(x.workDate)}`, meta: `${dtTime(x.workDate)} · ${x.departmentName || "-"}` })) : []),
          ...(Array.isArray(adjustmentAudit) ? adjustmentAudit.slice(0, 2).map(x => ({ title: `Adjustment: ${x.employeeFullName || x.employeeCode}`, subtitle: `${x.actionType} · ${x.requestedStatus}`, meta: `${dtTime(x.createdAt)} · ${x.note || 'Workflow event recorded.'}` })) : []),
          ...(Array.isArray(recentLeave) ? recentLeave.slice(0, 2).map(x => ({ title: `Leave: ${x.fullName}`, subtitle: `${x.leaveType} (${x.status})`, meta: `${dtTime(x.createdAt)} · ${dt(x.startDate)} → ${dt(x.endDate)}` })) : [])
        ].slice(0, 8);
        renderActivityItems("repActivityTimeline", timelineItems, "No recent operations to display.");

        renderActivityItems("repAdjustmentAuditHistory", (adjustmentAudit || []).map(item => ({
          title: `${item.actionType} · ${item.employeeFullName || '-'}`,
          subtitle: `${item.performedByUsername || 'System'} · ${dtTime(item.createdAt)}`,
          meta: `${dt(item.workDate)} · ${item.previousStatus || '-'} → ${item.newStatus || item.currentStatus || '-'}${item.note ? ` · ${item.note}` : ''}`
        })), 'No recent attendance-adjustment audit history is available.');

        renderActivityItems("repLeaveAuditHistory", (leaveAudit || []).map(item => ({
          title: `${item.actionType} · request #${item.leaveRequestId}`,
          subtitle: `${item.performedByUsername || 'System'} · ${dtTime(item.createdAt)}`,
          meta: `${item.previousStatus || '-'} → ${item.newStatus || '-'}${item.note ? ` · ${item.note}` : ''}`
        })), 'No recent leave audit history is available.');

        renderActivityItems("repPayrollAuditHistory", (payrollAudit || []).map(item => ({
          title: `${item.actionType} · ${item.employeeFullName || '-'}`,
          subtitle: `${item.performedByUsername || 'System'} · ${dtTime(item.createdAt)}`,
          meta: `${item.payrollMonth}/${item.payrollYear} · Net ${money(item.netSalary)}${item.note ? ` · ${item.note}` : ''}`
        })), 'No recent payroll audit history is available.');

        const scriptHost = q("reportScriptBody");
        if (scriptHost) {
          const profile = currentRoleProfile();
          scriptHost.innerHTML = `
            <ol class="mb-0">
              <li>For <strong>${esc(month.value)}/${esc(year.value)}</strong>, the ${esc(profile.workspace)} currently supports <strong>${overview.activeEmployees ?? 0}</strong> active employees and <strong>${overview.pendingLeaveRequests ?? 0}</strong> pending leave approvals.</li>
              <li>The attendance chart shows <strong>${attendance.presentCount ?? 0}</strong> present records, <strong>${attendance.lateCount ?? 0}</strong> late records, and <strong>${pendingAdjustments.length || 0}</strong> open attendance adjustments waiting for action.</li>
              <li>Payroll processing generated <strong>${payroll.totalPayrollRecords ?? 0}</strong> payroll entries with total net salary of <strong>${money(payroll.totalNetSalary)}</strong>.</li>
              <li>The leave workflow recorded <strong>${leave.totalRequests ?? 0}</strong> requests in the selected month, including <strong>${leave.approvedRequests ?? 0}</strong> approvals and <strong>${leave.pendingRequests ?? 0}</strong> pending decisions.</li>
              <li>The audit widgets now surface adjustment, leave, and payroll workflow history so the report page doubles as an operational evidence board.</li>
            </ol>`;
        }

        setDrillStatusOptions(reportState.drill.scope);
        renderDrillDown();
      } catch (err) {
        showError(err.message || "Failed to load reports.");
      }
    }

    async function runExport(url, successMessage, fallbackName) {
      if (exportMessage) exportMessage.textContent = "";
      try {
        await downloadFileFromApi(url, fallbackName);
        if (exportMessage) exportMessage.textContent = successMessage;
      } catch (err) {
        if (exportMessage) exportMessage.textContent = err.message || "Export failed.";
        showError(err.message || "Export failed.");
      }
    }

    function withFormat(url) {
      const selectedFormat = exportFormat?.value || 'csv';
      return url + `${url.includes('?') ? '&' : '?'}format=${encodeURIComponent(selectedFormat)}`;
    }

    function fallbackName(base) {
      const selectedFormat = exportFormat?.value || 'csv';
      return `${base}.${selectedFormat}`;
    }

    refresh?.addEventListener("click", load);
    refreshExportsBtn?.addEventListener("click", load);
    drillScope?.addEventListener('change', () => {
      reportState.drill.scope = drillScope.value;
      reportState.drill.status = '';
      reportState.drill.context = `Dataset changed to ${drillScope.options[drillScope.selectedIndex]?.text || drillScope.value}.`;
      setDrillStatusOptions(reportState.drill.scope);
      renderDrillDown();
    });
    drillStatus?.addEventListener('change', () => {
      reportState.drill.status = drillStatus.value;
      reportState.drill.context = `Status filter changed to ${drillStatus.options[drillStatus.selectedIndex]?.text || 'All statuses'}.`;
      renderDrillDown();
    });
    drillSearch?.addEventListener('input', () => {
      reportState.drill.search = drillSearch.value;
      renderDrillDown();
    });
    drillReset?.addEventListener('click', () => {
      reportState.drill = { scope: 'queue', status: '', search: '', context: 'Interactive drill-down was reset to the open approval queue.' };
      if (drillScope) drillScope.value = 'queue';
      if (drillSearch) drillSearch.value = '';
      setDrillStatusOptions('queue');
      renderDrillDown();
    });
    trendMetric?.addEventListener('change', () => {
      reportState.trendMetric = trendMetric.value || 'totalNetSalary';
      renderTrendCharts(reportState.data?.monthlyTrends || []);
    });
    trendReset?.addEventListener('click', () => {
      reportState.trendMetric = 'totalNetSalary';
      if (trendMetric) trendMetric.value = 'totalNetSalary';
      renderTrendCharts(reportState.data?.monthlyTrends || []);
    });
    exportSummaryBtn?.addEventListener("click", () => runExport(withFormat(`/api/Reports/monthly-summary/export?${buildQuery({ year: year.value, month: month.value })}`), "Monthly summary exported.", fallbackName(`monthly-summary-${year.value}-${month.value}`)));
    exportPayrollBtn?.addEventListener("click", () => runExport(withFormat(`/api/Reports/payrolls/export?${buildQuery({ year: year.value, month: month.value })}`), "Payroll report exported.", fallbackName(`payroll-report-${year.value}-${month.value}`)));
    exportLeaveBtn?.addEventListener("click", () => {
      const lastDay = new Date(Number(year.value), Number(month.value), 0).getDate();
      runExport(withFormat(`/api/Reports/leaves/export?${buildQuery({ fromDate: `${year.value}-${String(month.value).padStart(2, "0")}-01`, toDate: `${year.value}-${String(month.value).padStart(2, "0")}-${String(lastDay).padStart(2, "0")}` })}`), "Leave report exported.", fallbackName(`leave-report-${year.value}-${month.value}`));
    });
    exportAdjustmentsBtn?.addEventListener("click", () => runExport(withFormat(`/api/Reports/attendance-adjustments/export?${buildQuery({ year: year.value, month: month.value })}`), "Attendance-adjustment request report exported.", fallbackName(`attendance-adjustments-${year.value}-${month.value}`)));
    exportAdjustmentAuditBtn?.addEventListener("click", () => runExport(withFormat(`/api/Reports/attendance-adjustment-audit/export?${buildQuery({ year: year.value, month: month.value })}`), "Attendance-adjustment audit history exported.", fallbackName(`attendance-adjustment-audit-${year.value}-${month.value}`)));
    exportLeaveAuditBtn?.addEventListener("click", () => runExport(withFormat(`/api/Reports/leave-audit/export?${buildQuery({ year: year.value, month: month.value })}`), "Leave audit history exported.", fallbackName(`leave-audit-${year.value}-${month.value}`)));
    exportPayrollAuditBtn?.addEventListener("click", () => runExport(withFormat(`/api/Reports/payroll-audit/export?${buildQuery({ year: year.value, month: month.value })}`), "Payroll audit history exported.", fallbackName(`payroll-audit-${year.value}-${month.value}`)));
    aiRunBtn?.addEventListener('click', () => triggerAiSummary(false).catch(() => {}));
    aiUseScriptBtn?.addEventListener('click', () => triggerAiSummary(true).catch(() => {}));
    await setupAiPanel();
    renderAiSummaryResult('repAiResult', null, 'Reports assistant');
    await load();
  }

  async function openDetailModal(title, bodyHtml, showFooter = false) {
    let modalEl = q("sharedDetailModal");
    if (!modalEl) {
      const wrapper = document.createElement("div");
      wrapper.innerHTML = `
        <div class="modal fade" id="sharedDetailModal" tabindex="-1">
          <div class="modal-dialog modal-lg modal-dialog-scrollable">
            <div class="modal-content" style="border-radius:20px;">
              <div class="modal-header">
                <h5 class="modal-title" id="sharedDetailModalTitle"></h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
              </div>
              <div class="modal-body" id="sharedDetailModalBody"></div>
              <div class="modal-footer" id="sharedDetailModalFooter"></div>
            </div>
          </div>
        </div>`;
      document.body.appendChild(wrapper.firstElementChild);
      modalEl = q("sharedDetailModal");
    }
    q("sharedDetailModalTitle").textContent = title;
    q("sharedDetailModalBody").innerHTML = bodyHtml;
    q("sharedDetailModalFooter").style.display = showFooter ? "" : "none";
    if (!showFooter) q("sharedDetailModalFooter").innerHTML = "";
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();
  }

  async function openFormModal({ title, bodyHtml, submitText = "Save" }) {
    await openDetailModal(title, bodyHtml, true);
    const footer = q("sharedDetailModalFooter");
    footer.innerHTML = `
      <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
      <button type="button" class="btn btn-primary" id="sharedModalSubmitBtn">${esc(submitText)}</button>`;
    return q("sharedModalSubmitBtn");
  }

  function closeSharedModal() {
    const modalEl = q("sharedDetailModal");
    if (!modalEl) return;
    bootstrap.Modal.getOrCreateInstance(modalEl).hide();
  }

  function sharedLayoutBindings() {
    q("toolbarHomeLink")?.addEventListener("click", (e) => {
      e.preventDefault();
      window.location.href = "/admin/overview.html";
    });
  }

  async function init() {
    if (!guard()) return;
    if (!enforceRolePageAccess()) return;
    initShell();
    sharedLayoutBindings();

    switch (state.currentPage) {
      case "overview": await initOverview(); break;
      case "employees": await initEmployees(); break;
      case "attendances": await initAttendances(); break;
      case "payrolls": await initPayrolls(); break;
      case "leaves": await initLeaves(); break;
      case "departments": await initDepartments(); break;
      case "reports": await initReports(); break;
      default: break;
    }
  }

  return { init };
})();

document.addEventListener("DOMContentLoaded", AdminApp.init);
