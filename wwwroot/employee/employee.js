
const employeeToken = localStorage.getItem("token");
const employeeRole = localStorage.getItem("role");
const employeeUsername = localStorage.getItem("username");
const employeeCharts = {};

function employeeMoney(value) {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0
  }).format(value || 0);
}

function escapeHtml(text) {
  if (text == null) return "";
  return String(text)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function employeeStatusClass(status) {
  const v = (status || "").toLowerCase();
  if (v === "present") return "status-present";
  if (v === "late") return "status-late";
  if (v === "absent") return "status-absent";
  if (v === "leave") return "status-leave";
  if (v === "remote") return "status-remote";
  if (v === "approved") return "status-approved";
  if (v === "pending") return "status-pending";
  if (v === "rejected") return "status-rejected";
  if (v === "cancelled") return "status-cancelled";
  return "status-present";
}

function isEmployeePage() {
  return window.location.pathname.toLowerCase().includes("/employee/");
}

function ensureEmployeeGuard() {
  if (!isEmployeePage()) return true;

  const isLoginPage = window.location.pathname.toLowerCase().includes("/employee/login.html");

  if (!employeeToken || !employeeRole) {
    if (!isLoginPage) {
      window.location.href = "/employee/login.html";
      return false;
    }
    return true;
  }

  if (employeeRole === "Admin" || employeeRole === "HR" || employeeRole === "Manager") {
    window.location.href = "/admin/overview.html";
    return false;
  }

  if (employeeRole !== "Employee") {
    localStorage.removeItem("token");
    localStorage.removeItem("role");
    localStorage.removeItem("username");
    localStorage.removeItem("refreshToken");
    window.location.href = "/employee/login.html";
    return false;
  }

  if (isLoginPage) {
    window.location.href = "/employee/overview.html";
    return false;
  }

  return true;
}

async function apiGet(url) {
  const res = await fetch(url, {
    headers: { "Authorization": `Bearer ${employeeToken}` }
  });

  if (res.status === 401 || res.status === 403) {
    localStorage.removeItem("token");
    localStorage.removeItem("role");
    localStorage.removeItem("username");
    localStorage.removeItem("refreshToken");
    window.location.href = "/employee/login.html";
    throw new Error("Unauthorized");
  }

  if (!res.ok) throw new Error(`GET failed: ${res.status}`);
  return await res.json();
}

async function apiJson(url, method, body) {
  const res = await fetch(url, {
    method,
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${employeeToken}`
    },
    body: JSON.stringify(body)
  });

  let data = null;
  const text = await res.text();
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }

  if (res.status === 401 || res.status === 403) {
    localStorage.removeItem("token");
    localStorage.removeItem("role");
    localStorage.removeItem("username");
    localStorage.removeItem("refreshToken");
    window.location.href = "/employee/login.html";
    throw new Error("Unauthorized");
  }

  if (!res.ok) {
    const msg = typeof data === "string"
      ? data
      : data?.message || data?.title || `Request failed: ${res.status}`;
    throw new Error(msg);
  }

  return data;
}

function setSidebarActive() {
  const current = window.location.pathname.toLowerCase();
  document.querySelectorAll(".employee-nav-link").forEach(link => {
    const href = link.getAttribute("href").toLowerCase();
    link.classList.toggle("active", current.endsWith(href.replace("/employee/", "")) || current === href);
  });
}

function ensureEmployeeNavExtensions() {
  const nav = document.querySelector('.employee-nav');
  if (!nav) return;
  if (!nav.querySelector('[data-nav="overtime"]')) {
    const link = document.createElement('a');
    link.href = '/employee/overtime.html';
    link.className = 'employee-nav-link';
    link.dataset.nav = 'overtime';
    link.innerHTML = '<i class="bi bi-clock-history"></i><span>My Overtime</span>';
    const profile = nav.querySelector('[data-nav="profile"]');
    nav.insertBefore(link, profile || null);
  }
}

function initEmployeeShell() {
  const nameEl = document.getElementById("employeeSidebarName");
  const metaEl = document.getElementById("employeeSidebarMeta");
  const logoutBtn = document.getElementById("employeeLogoutBtn");
  const toggleBtn = document.getElementById("employeeMenuToggle");
  const backdrop = document.getElementById("employeeBackdrop");
  const shell = document.getElementById("employeeShell");

  if (nameEl) nameEl.textContent = employeeUsername || "Employee";
  if (metaEl) metaEl.textContent = "Loading profile...";

  if (logoutBtn) {
    logoutBtn.addEventListener("click", () => {
      localStorage.removeItem("token");
      localStorage.removeItem("role");
      localStorage.removeItem("username");
    localStorage.removeItem("refreshToken");
      window.location.href = "/employee/login.html";
    });
  }

  if (toggleBtn && shell) {
    toggleBtn.addEventListener("click", () => {
      shell.classList.toggle("sidebar-open");
    });
  }

  if (backdrop && shell) {
    backdrop.addEventListener("click", () => {
      shell.classList.remove("sidebar-open");
    });
  }

  ensureEmployeeNavExtensions();
  setSidebarActive();
  loadSidebarProfile();
}

async function loadSidebarProfile() {
  try {
    const profile = await apiGet("/api/Employees/my-profile");
    const nameEl = document.getElementById("employeeSidebarName");
    const metaEl = document.getElementById("employeeSidebarMeta");

    if (nameEl) nameEl.textContent = profile.fullName || employeeUsername || "Employee";
    if (metaEl) {
      const code = profile.employeeCode || "-";
      const dept = profile.departmentName || "-";
      metaEl.textContent = `${code} / ${dept}`;
    }
  } catch (err) {
    console.error(err);
  }
}

function formatDate(value) {
  if (!value) return "-";
  return new Date(value).toLocaleDateString();
}

function formatTime(value) {
  if (!value) return "-";
  return new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function formatDateTime(value) {
  if (!value) return "-";
  const d = new Date(value);
  return `${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
}


function employeeDownloadFile(fileName, content, mimeType = 'text/plain;charset=utf-8') {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function employeeExportCsv(fileName, headers, rows) {
  const escape = value => `"${String(value ?? '').replaceAll('"', '""')}"`;
  const lines = [headers.map(escape).join(',')].concat(rows.map(row => row.map(escape).join(',')));
  employeeDownloadFile(fileName, lines.join('\n'), 'text/csv;charset=utf-8');
}

function employeeOpenPrintReport(title, subtitle, headers, rows) {
  const html = `<!DOCTYPE html><html><head><meta charset="UTF-8"><title>${escapeHtml(title)}</title><style>
    body{font-family:Arial,sans-serif;padding:24px;color:#0f172a} h1{margin:0 0 6px;font-size:24px} p{margin:0 0 18px;color:#475569}
    table{width:100%;border-collapse:collapse;font-size:12px} th,td{border:1px solid #cbd5e1;padding:8px;vertical-align:top;text-align:left} th{background:#eff6ff}
  </style></head><body><h1>${escapeHtml(title)}</h1><p>${escapeHtml(subtitle)}</p><table><thead><tr>${headers.map(h => `<th>${escapeHtml(h)}</th>`).join('')}</tr></thead><tbody>${rows.length ? rows.map(row => `<tr>${row.map(cell => `<td>${escapeHtml(cell)}</td>`).join('')}</tr>`).join('') : `<tr><td colspan="${headers.length}">No records available.</td></tr>`}</tbody></table></body></html>`;
  const popup = window.open('', '_blank', 'width=1080,height=760');
  if (!popup) return;
  popup.document.open();
  popup.document.write(html);
  popup.document.close();
  popup.focus();
  popup.print();
}

function loadEmployeeLoginPage() {
  const form = document.getElementById("employeeLoginForm");
  const errorEl = document.getElementById("loginError");
  if (!form) return;

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    errorEl.textContent = "";

    const username = document.getElementById("username").value.trim();
    const password = document.getElementById("password").value;

    try {
      const res = await fetch("/api/Auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password })
      });

      const data = await res.json();

      if (!res.ok) {
        errorEl.textContent = data?.message || data?.title || "Login failed.";
        return;
      }

      if (data.role !== "Employee") {
        errorEl.textContent = "This page is for Employee accounts only.";
        return;
      }

      localStorage.setItem("token", data.token);
      localStorage.setItem("role", data.role);
      localStorage.setItem("username", data.username || username);
      if (data.refreshToken) localStorage.setItem("refreshToken", data.refreshToken);

      window.location.href = "/employee/overview.html";
    } catch (err) {
      console.error(err);
      errorEl.textContent = "Cannot connect to server.";
    }
  });
}

async function fetchEmployeeLeaveHistory(id) {
  try {
    const items = await apiGet(`/api/LeaveRequests/${id}/history`);
    return Array.isArray(items) ? items : [];
  } catch {
    return [];
  }
}

async function fetchEmployeeAdjustmentHistory(id) {
  try {
    const items = await apiGet(`/api/Attendances/adjustment-requests/${id}/history`);
    return Array.isArray(items) ? items : [];
  } catch {
    return [];
  }
}

function renderEmployeeChart(key, canvasId, config) {
  const canvas = document.getElementById(canvasId);
  if (!canvas || typeof Chart === "undefined") return;
  if (employeeCharts[key]) employeeCharts[key].destroy();
  employeeCharts[key] = new Chart(canvas, config);
}

async function loadOverviewPage() {
  const [profile, attendances, payrolls, leaves, adjustments, adjustmentAudit] = await Promise.all([
    apiGet("/api/Employees/my-profile"),
    apiGet("/api/Attendances/my-attendances"),
    apiGet("/api/Payrolls/my-payrolls").catch(() => []),
    apiGet("/api/LeaveRequests/my-requests"),
    apiGet("/api/Attendances/my-adjustment-requests").catch(() => []),
    apiGet("/api/Attendances/my-adjustment-history/recent?take=8").catch(() => [])
  ]);

  document.getElementById("overviewEmployeeName").textContent = profile.fullName || "-";
  document.getElementById("overviewDepartmentTag").innerHTML = `<i class="bi bi-building"></i> ${escapeHtml(profile.departmentName || "-")}`;
  document.getElementById("overviewPositionTag").innerHTML = `<i class="bi bi-briefcase"></i> ${escapeHtml(profile.positionName || "-")}`;
  document.getElementById("overviewCodeTag").innerHTML = `<i class="bi bi-person-badge"></i> ${escapeHtml(profile.employeeCode || "-")}`;

  const latestPayroll = Array.isArray(payrolls) && payrolls.length ? payrolls[0] : null;
  const pendingLeaves = Array.isArray(leaves) ? leaves.filter(x => (x.status || "").toLowerCase() === "pending").length : 0;
  const pendingAdjustments = Array.isArray(adjustments) ? adjustments.filter(x => (x.status || "").toLowerCase() === "pending").length : 0;
  const lateRecords = Array.isArray(attendances) ? attendances.filter(x => (x.status || "").toLowerCase() === "late").length : 0;
  const absentRecords = Array.isArray(attendances) ? attendances.filter(x => (x.status || "").toLowerCase() === "absent").length : 0;
  const reliableDays = Array.isArray(attendances) ? attendances.filter(x => ['present', 'remote'].includes((x.status || '').toLowerCase())).length : 0;
  const approvedLeaveCount = Array.isArray(leaves) ? leaves.filter(x => (x.status || "").toLowerCase() === "approved").length : 0;
  const approvedAdjustmentCount = Array.isArray(adjustments) ? adjustments.filter(x => (x.status || "").toLowerCase() === "approved").length : 0;
  const approvedLeaveDays = Array.isArray(leaves) ? leaves.filter(x => (x.status || '').toLowerCase() === 'approved').reduce((sum, x) => sum + Number(x.totalDays || 0), 0) : 0;
  const closedLeaveCount = Array.isArray(leaves) ? leaves.filter(x => ['rejected', 'cancelled'].includes((x.status || '').toLowerCase())).length : 0;
  const totalNetSalary = Array.isArray(payrolls) ? payrolls.reduce((sum, x) => sum + Number(x.netSalary || 0), 0) : 0;
  const averageNetSalary = payrolls.length ? totalNetSalary / payrolls.length : 0;
  const highestNetSalary = payrolls.length ? Math.max(...payrolls.map(x => Number(x.netSalary || 0))) : 0;
  const averageHours = attendances.length
    ? attendances.reduce((sum, x) => sum + Number(x.workingHours || 0), 0) / attendances.length
    : 0;

  document.getElementById("overviewMonthlyAttendance").textContent = attendances.length || 0;
  document.getElementById("overviewPendingLeaves").textContent = pendingLeaves + pendingAdjustments;
  document.getElementById("overviewLatestPayroll").textContent = latestPayroll ? employeeMoney(latestPayroll.netSalary) : "-";
  document.getElementById("overviewProfileStatus").textContent = profile.isActive ? "Active" : "Inactive";

  document.getElementById("kpiThisMonthRecords").textContent = attendances.length || 0;
  document.getElementById("kpiLateRecords").textContent = lateRecords;
  document.getElementById("kpiLeaveRequests").textContent = leaves.length || 0;
  document.getElementById("kpiPayrollRecords").textContent = payrolls.length || 0;
  document.getElementById("overviewAdjustmentCount").textContent = adjustments.length || 0;
  document.getElementById("overviewApprovedActionCount").textContent = approvedLeaveCount + approvedAdjustmentCount;

  const setText = (id, value) => {
    const el = document.getElementById(id);
    if (el) el.textContent = value;
  };
  setText('overviewAttendanceReliable', reliableDays);
  setText('overviewAttendanceAverageHours', `${averageHours.toFixed(1)}h`);
  setText('overviewAttendanceFlags', lateRecords + absentRecords);
  setText('overviewLeaveApprovedDays', approvedLeaveDays);
  setText('overviewLeavePendingCount', pendingLeaves);
  setText('overviewLeaveClosedCount', closedLeaveCount);
  setText('overviewPayrollTotalNet', employeeMoney(totalNetSalary));
  setText('overviewPayrollAverageNet', employeeMoney(averageNetSalary));
  setText('overviewPayrollHighestNet', employeeMoney(highestNetSalary));

  const attendanceFocus = pendingAdjustments
    ? `You still have ${pendingAdjustments} attendance adjustment request(s) waiting for review.`
    : (lateRecords + absentRecords)
      ? `There are ${lateRecords + absentRecords} flagged attendance record(s). Review your daily records if anything needs correction.`
      : `Attendance looks stable with ${reliableDays} reliable day(s) and an average of ${averageHours.toFixed(1)} working hours.`;
  const leaveFocus = pendingLeaves
    ? `${pendingLeaves} leave request(s) are still waiting for a decision, while ${approvedLeaveDays} approved leave day(s) have already been cleared.`
    : closedLeaveCount
      ? `${closedLeaveCount} leave request(s) were already closed by rejection or cancellation, so your workflow history is fully traceable.`
      : `No leave request is currently waiting for review. Approved leave days recorded so far: ${approvedLeaveDays}.`;
  const payrollFocus = payrolls.length
    ? `Payroll history currently totals ${employeeMoney(totalNetSalary)} with a highest single release of ${employeeMoney(highestNetSalary)}.`
    : 'No payroll release is available yet in your self-service history.';

  setText('overviewAttendanceFocusNote', attendanceFocus);
  setText('overviewLeaveFocusNote', leaveFocus);
  setText('overviewPayrollFocusNote', payrollFocus);

  const attBody = document.getElementById("overviewAttendanceBody");
  attBody.innerHTML = attendances.length
    ? attendances.slice(0, 5).map(x => `
      <tr>
        <td>${formatDate(x.workDate)}</td>
        <td><span class="status-pill ${employeeStatusClass(x.status)}">${escapeHtml(x.status)}</span></td>
        <td>${x.workingHours ?? "-"}</td>
      </tr>`).join("")
    : `<tr><td colspan="3" class="text-center text-muted py-4">No attendance data</td></tr>`;

  const payrollBody = document.getElementById("overviewPayrollBody");
  payrollBody.innerHTML = payrolls.length
    ? payrolls.slice(0, 5).map(x => `
      <tr>
        <td>${x.payrollMonth}/${x.payrollYear}</td>
        <td>${employeeMoney(x.netSalary)}</td>
      </tr>`).join("")
    : `<tr><td colspan="2" class="text-center text-muted py-4">No payroll data</td></tr>`;

  const leaveBody = document.getElementById("overviewLeaveBody");
  leaveBody.innerHTML = leaves.length
    ? leaves.slice(0, 5).map(x => `
      <tr>
        <td>${escapeHtml(x.leaveType)}</td>
        <td><span class="status-pill ${employeeStatusClass(x.status)}">${escapeHtml(x.status)}</span></td>
        <td>${formatDate(x.startDate)} - ${formatDate(x.endDate)}</td>
      </tr>`).join("")
    : `<tr><td colspan="3" class="text-center text-muted py-4">No leave data</td></tr>`;

  const adjustmentBody = document.getElementById("overviewAdjustmentBody");
  if (adjustmentBody) {
    adjustmentBody.innerHTML = adjustments.length
      ? adjustments.slice(0, 5).map(x => `
        <tr>
          <td>${formatDate(x.workDate)}</td>
          <td><span class="status-pill ${employeeStatusClass(x.requestedStatus)}">${escapeHtml(x.requestedStatus)}</span></td>
          <td><span class="status-pill ${employeeStatusClass(x.status)}">${escapeHtml(x.status)}</span></td>
          <td>${x.reviewedAt ? `${escapeHtml(x.reviewedByUsername || '-')}` : '<span class="text-muted">Pending</span>'}</td>
        </tr>`).join("")
      : `<tr><td colspan="4" class="text-center text-muted py-4">No adjustment requests yet</td></tr>`;
  }

  const [leaveHistories, adjustmentHistories] = await Promise.all([
    Promise.all((leaves || []).slice(0, 4).map(item => fetchEmployeeLeaveHistory(item.id))),
    Promise.all((adjustments || []).slice(0, 4).map(item => fetchEmployeeAdjustmentHistory(item.id)))
  ]);

  const timelineItems = [];
  (attendances || []).slice(0, 4).forEach(item => {
    timelineItems.push({
      at: new Date(item.workDate).getTime(),
      icon: 'bi-calendar2-check',
      title: `Attendance recorded: ${item.status}`,
      subtitle: `${formatDate(item.workDate)} · ${item.workingHours ?? '-'} hours`,
      meta: item.note || 'Attendance status captured in your personal record.'
    });
  });
  (payrolls || []).slice(0, 4).forEach(item => {
    const stamp = item.generatedAt ? new Date(item.generatedAt).getTime() : new Date(item.payrollYear, (item.payrollMonth || 1) - 1, 1).getTime();
    timelineItems.push({
      at: stamp,
      icon: 'bi-wallet2',
      title: `Payroll released for ${item.payrollMonth}/${item.payrollYear}`,
      subtitle: `${employeeMoney(item.netSalary)} net salary`,
      meta: `Generated ${formatDateTime(item.generatedAt)}`
    });
  });
  leaveHistories.forEach(historyList => {
    (historyList || []).slice(0, 1).forEach(item => {
      timelineItems.push({
        at: new Date(item.createdAt).getTime(),
        icon: 'bi-journal-check',
        title: `Leave workflow: ${item.actionType}`,
        subtitle: `${item.newStatus || item.previousStatus || '-'} · ${formatDateTime(item.createdAt)}`,
        meta: item.note || 'Leave workflow updated.'
      });
    });
  });
  adjustmentHistories.forEach(historyList => {
    (historyList || []).slice(0, 2).forEach(item => {
      timelineItems.push({
        at: new Date(item.createdAt).getTime(),
        icon: 'bi-arrow-repeat',
        title: `Attendance adjustment: ${item.actionType}`,
        subtitle: `${item.currentStatus || item.newStatus || '-'} · ${formatDate(item.workDate)}`,
        meta: item.note || 'Attendance-adjustment workflow updated.'
      });
    });
  });
  (adjustmentAudit || []).slice(0, 3).forEach(item => {
    timelineItems.push({
      at: new Date(item.createdAt).getTime(),
      icon: 'bi-clipboard-check',
      title: `Adjustment audit: ${item.actionType}`,
      subtitle: `${item.currentStatus || '-'} · ${formatDateTime(item.createdAt)}`,
      meta: item.note || 'Adjustment audit trail entry.'
    });
  });

  timelineItems.sort((a, b) => (b.at || 0) - (a.at || 0));
  const latestAction = timelineItems[0];
  const actionTag = document.getElementById('overviewLastActionTag');
  if (actionTag) {
    actionTag.innerHTML = `<i class="bi bi-lightning-charge"></i> ${escapeHtml(latestAction ? latestAction.title : 'No recent action')}`;
  }

  const timelineHost = document.getElementById('employeeTimelineHost');
  if (timelineHost) {
    timelineHost.innerHTML = timelineItems.length
      ? timelineItems.slice(0, 8).map(item => `
        <div class="employee-timeline-item">
          <div class="employee-timeline-icon"><i class="bi ${item.icon}"></i></div>
          <div class="employee-timeline-copy">
            <strong>${escapeHtml(item.title)}</strong>
            <span>${escapeHtml(item.subtitle)}</span>
            <small>${escapeHtml(item.meta)}</small>
          </div>
        </div>`).join('')
      : `<div class="text-muted">No self-service timeline events yet.</div>`;
  }

  renderEmployeeChart('attendanceOverview', 'employeeAttendanceChart', {
    type: 'doughnut',
    data: {
      labels: ['Present', 'Late', 'Absent', 'Leave', 'Remote'],
      datasets: [{
        data: [
          attendances.filter(x => (x.status || '').toLowerCase() === 'present').length,
          attendances.filter(x => (x.status || '').toLowerCase() === 'late').length,
          attendances.filter(x => (x.status || '').toLowerCase() === 'absent').length,
          attendances.filter(x => (x.status || '').toLowerCase() === 'leave').length,
          attendances.filter(x => (x.status || '').toLowerCase() === 'remote').length
        ],
        backgroundColor: ['rgba(37,99,235,.82)','rgba(245,158,11,.82)','rgba(239,68,68,.82)','rgba(139,92,246,.82)','rgba(16,185,129,.82)'],
        borderWidth: 0
      }]
    },
    options: { maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } } }
  });

  const workflowCounts = {
    Pending: pendingLeaves + pendingAdjustments,
    Approved: approvedLeaveCount + approvedAdjustmentCount,
    Rejected: (leaves || []).filter(x => (x.status || '').toLowerCase() === 'rejected').length + (adjustments || []).filter(x => (x.status || '').toLowerCase() === 'rejected').length,
    Cancelled: (leaves || []).filter(x => (x.status || '').toLowerCase() === 'cancelled').length
  };
  renderEmployeeChart('requestOverview', 'employeeRequestChart', {
    type: 'bar',
    data: {
      labels: Object.keys(workflowCounts),
      datasets: [{
        label: 'Workflow items',
        data: Object.values(workflowCounts),
        backgroundColor: ['rgba(245,158,11,.82)','rgba(16,185,129,.82)','rgba(239,68,68,.82)','rgba(100,116,139,.82)'],
        borderRadius: 12,
        maxBarThickness: 42
      }]
    },
    options: { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
  });

  const payrollTrend = [...(payrolls || [])]
    .sort((a, b) => (a.payrollYear - b.payrollYear) || (a.payrollMonth - b.payrollMonth))
    .slice(-6);
  renderEmployeeChart('payrollOverview', 'employeePayrollChart', {
    type: 'line',
    data: {
      labels: payrollTrend.map(x => `${x.payrollMonth}/${x.payrollYear}`),
      datasets: [{
        label: 'Net salary',
        data: payrollTrend.map(x => x.netSalary || 0),
        borderColor: 'rgba(37,99,235,.82)',
        backgroundColor: 'rgba(37,99,235,.15)',
        tension: .35,
        fill: true
      }]
    },
    options: { maintainAspectRatio: false, plugins: { legend: { display: false } } }
  });
}

async function loadAttendancesPage() {
  const monthInput = document.getElementById("attendanceMonth");
  const yearInput = document.getElementById("attendanceYear");
  const workDateInput = document.getElementById("attendanceWorkDate");
  const statusInput = document.getElementById("attendanceStatus");
  const searchInput = document.getElementById("attendanceSearchInput");
  const pageSizeInput = document.getElementById("attendancePageSize");
  const filterBtn = document.getElementById("attendanceFilterBtn");
  const resetBtn = document.getElementById("attendanceResetBtn");
  const exportCsvBtn = document.getElementById("attendanceExportCsvBtn");
  const exportPrintBtn = document.getElementById("attendanceExportPrintBtn");
  const statsHost = document.getElementById("employeeAttendanceStats");
  const highlightHost = document.getElementById("employeeAttendanceHighlights");
  const adjustmentForm = document.getElementById("attendanceAdjustmentForm");
  const adjustmentMessage = document.getElementById("attendanceAdjustmentMessage");

  const myAdjustmentStatusFilter = document.getElementById("myAdjustmentStatusFilter");
  const myAdjustmentMonthFilter = document.getElementById("myAdjustmentMonthFilter");
  const myAdjustmentYearFilter = document.getElementById("myAdjustmentYearFilter");
  const adjustmentSearchInput = document.getElementById("adjustmentSearchInput");
  const adjustmentPageSizeInput = document.getElementById("adjustmentPageSize");
  const myAdjustmentApplyBtn = document.getElementById("myAdjustmentApplyBtn");
  const myAdjustmentResetBtn = document.getElementById("myAdjustmentResetBtn");
  const adjustmentExportCsvBtn = document.getElementById("adjustmentExportCsvBtn");
  const adjustmentExportPrintBtn = document.getElementById("adjustmentExportPrintBtn");
  const adjustmentStats = document.getElementById("employeeAdjustmentStats");
  const adjustmentPaging = document.getElementById("employeeAdjustmentPaging");
  const attendancePaging = document.getElementById("employeeAttendancePaging");
  const historyPanel = document.getElementById("employeeAdjustmentHistoryPanel");

  const now = new Date();
  monthInput.value = now.getMonth() + 1;
  yearInput.value = now.getFullYear();
  if (myAdjustmentMonthFilter) myAdjustmentMonthFilter.value = now.getMonth() + 1;
  if (myAdjustmentYearFilter) myAdjustmentYearFilter.value = now.getFullYear();

  const paging = {
    attendances: { page: 1, pageSize: Number(pageSizeInput?.value || 8) },
    adjustments: { page: 1, pageSize: Number(adjustmentPageSizeInput?.value || 6) }
  };
  let selectedAdjustmentId = null;
  let attendanceCache = [];
  let adjustmentCache = [];

  const hoursNumber = value => {
    const n = Number(value);
    return Number.isFinite(n) ? n : 0;
  };
  const formatHoursLabel = value => `${hoursNumber(value).toFixed(1)} hrs`;

  function paginate(items, cfg) {
    const totalPages = Math.max(1, Math.ceil(items.length / cfg.pageSize));
    cfg.page = Math.min(cfg.page, totalPages);
    const start = (cfg.page - 1) * cfg.pageSize;
    return { totalPages, pageItems: items.slice(start, start + cfg.pageSize) };
  }

  function renderPager(host, cfg, totalItems, label, onRender) {
    if (!host) return;
    const totalPages = Math.max(1, Math.ceil(totalItems / cfg.pageSize));
    if (cfg.page > totalPages) cfg.page = totalPages;
    host.innerHTML = `
      <div class="page-bar employee-page-bar">
        <div class="small text-muted">Page <strong>${cfg.page}</strong> / <strong>${totalPages}</strong> · ${totalItems} ${label}</div>
        <div class="page-controls d-flex gap-2 flex-wrap">
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page <= 1 ? "disabled" : ""} data-page-action="first">First</button>
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page <= 1 ? "disabled" : ""} data-page-action="prev">Prev</button>
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page >= totalPages ? "disabled" : ""} data-page-action="next">Next</button>
          <button class="btn btn-outline-secondary btn-sm" ${cfg.page >= totalPages ? "disabled" : ""} data-page-action="last">Last</button>
        </div>
      </div>`;
    host.querySelectorAll("[data-page-action]").forEach(btn => {
      btn.addEventListener("click", () => {
        if (btn.dataset.pageAction === "first") cfg.page = 1;
        if (btn.dataset.pageAction === "prev") cfg.page = Math.max(1, cfg.page - 1);
        if (btn.dataset.pageAction === "next") cfg.page = Math.min(totalPages, cfg.page + 1);
        if (btn.dataset.pageAction === "last") cfg.page = totalPages;
        onRender();
      });
    });
  }

  function syncAdjustmentDateTimeDefaults() {
    const workDate = document.getElementById("adjustmentWorkDateInput")?.value;
    const status = document.getElementById("adjustmentStatusInput")?.value || "Present";
    const checkInInput = document.getElementById("adjustmentCheckInInput");
    const checkOutInput = document.getElementById("adjustmentCheckOutInput");
    if (!checkInInput || !checkOutInput) return;

    const requiresWorkTime = ["Present", "Late", "Remote"].includes(status);
    checkInInput.required = requiresWorkTime;

    if (workDate && requiresWorkTime) {
      if (!checkInInput.value) checkInInput.value = `${workDate}T08:00`;
      if (!checkOutInput.value) checkOutInput.value = `${workDate}T17:00`;
    }

    if (!requiresWorkTime) {
      checkOutInput.value = "";
    }
  }

  function getFilteredAttendances() {
    const keyword = String(searchInput?.value || "").trim().toLowerCase();
    return attendanceCache
      .filter(item => {
        const haystack = [
          formatDate(item.workDate),
          item.status,
          item.note,
          item.checkInTime ? formatDateTime(item.checkInTime) : '',
          item.checkOutTime ? formatDateTime(item.checkOutTime) : '',
          item.workingHours,
          item.sourceType
        ].join(' ').toLowerCase();
        return !keyword || haystack.includes(keyword);
      })
      .sort((a, b) => new Date(b.workDate) - new Date(a.workDate));
  }

  function getFilteredAdjustments() {
    const keyword = String(adjustmentSearchInput?.value || "").trim().toLowerCase();
    return adjustmentCache
      .filter(item => {
        const haystack = [
          item.requestedStatus,
          item.status,
          item.reason,
          item.reviewNote,
          item.reviewedByUsername,
          formatDate(item.workDate),
          item.requestedCheckInTime ? formatDateTime(item.requestedCheckInTime) : '',
          item.requestedCheckOutTime ? formatDateTime(item.requestedCheckOutTime) : ''
        ].join(' ').toLowerCase();
        return !keyword || haystack.includes(keyword);
      })
      .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
  }

  function renderAttendanceDashboard() {
    const items = getFilteredAttendances();
    const adjustments = getFilteredAdjustments();
    const presentCount = items.filter(x => (x.status || "").toLowerCase() === "present").length;
    const remoteCount = items.filter(x => (x.status || "").toLowerCase() === "remote").length;
    const lateCount = items.filter(x => (x.status || "").toLowerCase() === "late").length;
    const absentCount = items.filter(x => (x.status || "").toLowerCase() === "absent").length;
    const leaveCount = items.filter(x => (x.status || "").toLowerCase() === "leave").length;
    const totalHours = items.reduce((sum, item) => sum + hoursNumber(item.workingHours), 0);
    const avgHours = items.length ? totalHours / items.length : 0;
    const pendingAdjustments = adjustments.filter(x => (x.status || '').toLowerCase() === 'pending').length;
    const approvedAdjustments = adjustments.filter(x => (x.status || '').toLowerCase() === 'approved').length;
    const rejectedAdjustments = adjustments.filter(x => (x.status || '').toLowerCase() === 'rejected').length;

    if (statsHost) {
      statsHost.innerHTML = `
        <span class="status-chip status-approved">Reliable ${presentCount + remoteCount}</span>
        <span class="status-chip status-pending">Late ${lateCount}</span>
        <span class="status-chip status-rejected">Absent ${absentCount}</span>`;
    }

    const kpiRecords = document.getElementById('attendanceKpiRecords');
    const kpiReliable = document.getElementById('attendanceKpiReliable');
    const kpiFlags = document.getElementById('attendanceKpiFlags');
    const kpiAverageHours = document.getElementById('attendanceKpiAverageHours');
    const kpiAdjustments = document.getElementById('attendanceKpiAdjustments');
    const kpiFocus = document.getElementById('attendanceKpiFocus');
    if (kpiRecords) kpiRecords.textContent = items.length;
    if (kpiReliable) kpiReliable.textContent = presentCount + remoteCount;
    if (kpiFlags) kpiFlags.textContent = lateCount + absentCount;
    if (kpiAverageHours) kpiAverageHours.textContent = formatHoursLabel(avgHours);
    if (kpiAdjustments) kpiAdjustments.textContent = adjustments.length;
    if (kpiFocus) {
      const focusBits = [];
      if (workDateInput?.value) focusBits.push(`Focused on ${workDateInput.value}`);
      else if (monthInput?.value && yearInput?.value) focusBits.push(`Focused on ${String(monthInput.value).padStart(2, '0')}/${yearInput.value}`);
      if (statusInput?.value) focusBits.push(`Status ${statusInput.value}`);
      if (searchInput?.value) focusBits.push(`Search “${searchInput.value.trim()}”`);
      kpiFocus.textContent = focusBits.length ? focusBits.join(' · ') : 'Attendance snapshot across the selected period';
    }

    if (highlightHost) {
      const latest = items[0];
      const longest = [...items].sort((a, b) => hoursNumber(b.workingHours) - hoursNumber(a.workingHours))[0];
      const highlightCards = [
        latest ? { title: 'Latest attendance capture', text: `${formatDate(latest.workDate)} is marked as ${latest.status} with ${latest.workingHours ?? 0} working hours.` } : null,
        longest ? { title: 'Strongest working-day total', text: `${formatDate(longest.workDate)} currently has the highest visible working-hour total at ${formatHoursLabel(longest.workingHours)}.` } : null,
        { title: 'Adjustment workflow snapshot', text: `${pendingAdjustments} pending, ${approvedAdjustments} approved, and ${rejectedAdjustments} rejected correction requests are in your current filtered view.` },
        { title: 'Attendance balance', text: `${presentCount + remoteCount} reliable records, ${lateCount} late records, ${absentCount} absences, and ${leaveCount} leave-generated entries are visible right now.` }
      ].filter(Boolean);
      highlightHost.innerHTML = highlightCards.map(card => `<div class="employee-highlight-card"><strong>${escapeHtml(card.title)}</strong><span>${escapeHtml(card.text)}</span></div>`).join('');
    }

    renderEmployeeChart('employeeAttendanceStatus', 'employeeAttendanceStatusChart', {
      type: 'doughnut',
      data: {
        labels: ['Present', 'Late', 'Absent', 'Leave', 'Remote'],
        datasets: [{ data: [presentCount, lateCount, absentCount, leaveCount, remoteCount], backgroundColor: ['rgba(37,99,235,.82)','rgba(245,158,11,.82)','rgba(239,68,68,.82)','rgba(168,85,247,.82)','rgba(20,184,166,.82)'], borderWidth: 0 }]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } } }
    });

    const trendItems = [...items].sort((a, b) => new Date(a.workDate) - new Date(b.workDate)).slice(-10);
    renderEmployeeChart('employeeAttendanceHoursTrend', 'employeeAttendanceHoursTrendChart', {
      type: 'line',
      data: {
        labels: trendItems.map(item => formatDate(item.workDate)),
        datasets: [{ label: 'Working hours', data: trendItems.map(item => hoursNumber(item.workingHours)), borderColor: 'rgba(14,116,144,.95)', backgroundColor: 'rgba(14,116,144,.18)', fill: true, tension: 0.3, borderWidth: 3, pointRadius: 4 }]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true } } }
    });

    renderEmployeeChart('employeeAttendanceAdjustment', 'employeeAttendanceAdjustmentChart', {
      type: 'bar',
      data: {
        labels: ['Pending', 'Approved', 'Rejected'],
        datasets: [{ label: 'Requests', data: [pendingAdjustments, approvedAdjustments, rejectedAdjustments], backgroundColor: ['rgba(245,158,11,.82)','rgba(16,185,129,.82)','rgba(239,68,68,.82)'], borderRadius: 12, borderSkipped: false }]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
    });
  }

  async function renderAdjustmentHistory(requestId) {
    if (!historyPanel) return;
    if (!requestId) {
      historyPanel.innerHTML = `<div class="empty-state">Select a request to inspect its audit history.</div>`;
      return;
    }
    const request = adjustmentCache.find(x => String(x.id) === String(requestId));
    if (!request) {
      historyPanel.innerHTML = `<div class="empty-state">The selected request is no longer available in this filtered list.</div>`;
      return;
    }
    const history = await fetchEmployeeAdjustmentHistory(requestId);
    historyPanel.innerHTML = `
      <div class="employee-leave-history-header">
        <div>
          <h5 class="mb-1">Adjustment Request #${request.id}</h5>
          <div class="small text-muted">${formatDate(request.workDate)} · ${escapeHtml(request.requestedStatus)} · Submitted ${formatDateTime(request.createdAt)}</div>
        </div>
        <span class="status-pill ${employeeStatusClass(request.status)}">${escapeHtml(request.status)}</span>
      </div>
      <div class="history-meta-grid">
        <div class="history-meta-card"><span>Requested Check In</span><strong>${request.requestedCheckInTime ? formatDateTime(request.requestedCheckInTime) : '-'}</strong></div>
        <div class="history-meta-card"><span>Requested Check Out</span><strong>${request.requestedCheckOutTime ? formatDateTime(request.requestedCheckOutTime) : '-'}</strong></div>
        <div class="history-meta-card"><span>Review Snapshot</span><strong>${escapeHtml(request.reviewNote || (request.reviewedByUsername ? `Reviewed by ${request.reviewedByUsername}` : 'Pending review'))}</strong></div>
      </div>
      <div class="employee-timeline-list">
        ${history.length ? history.map(item => `
          <div class="employee-timeline-item compact">
            <div class="employee-timeline-icon"><i class="bi bi-arrow-repeat"></i></div>
            <div class="employee-timeline-copy">
              <strong>${escapeHtml(item.actionType || '-')}</strong>
              <span>${escapeHtml(item.performedByUsername || 'System')} · ${formatDateTime(item.createdAt)}</span>
              <small>${escapeHtml(item.previousStatus || '-')} → ${escapeHtml(item.newStatus || item.currentStatus || '-')} · ${escapeHtml(item.note || 'No additional note.')}</small>
            </div>
          </div>`).join('') : `<div class="empty-state">No audit trail is available for this request yet.</div>`}
      </div>`;
  }

  function renderAttendances() {
    const items = getFilteredAttendances();
    const { pageItems } = paginate(items, paging.attendances);
    const body = document.getElementById("attendanceTableBody");
    body.innerHTML = pageItems.length
      ? pageItems.map(x => `
        <tr>
          <td>${formatDate(x.workDate)}</td>
          <td>${formatTime(x.checkInTime)}</td>
          <td>${formatTime(x.checkOutTime)}</td>
          <td><span class="status-pill ${employeeStatusClass(x.status)}">${escapeHtml(x.status)}</span></td>
          <td>${x.workingHours ?? "-"}</td>
          <td>${escapeHtml(x.note || "-")}</td>
        </tr>`).join("")
      : `<tr><td colspan="6" class="text-center text-muted py-4">No attendance records found</td></tr>`;
    renderPager(attendancePaging, paging.attendances, items.length, 'attendance record(s)', renderAttendances);
  }

  function renderAdjustmentRequests() {
    const items = getFilteredAdjustments();
    const { pageItems } = paginate(items, paging.adjustments);
    const requestBody = document.getElementById("attendanceAdjustmentTableBody");
    requestBody.innerHTML = pageItems.length
      ? pageItems.map(x => `
        <tr class="${String(selectedAdjustmentId) === String(x.id) ? 'employee-row-selected' : ''}">
          <td>${formatDateTime(x.createdAt)}</td>
          <td>${formatDate(x.workDate)}</td>
          <td><span class="status-pill ${employeeStatusClass(x.requestedStatus)}">${escapeHtml(x.requestedStatus)}</span></td>
          <td>${x.requestedCheckInTime ? formatDateTime(x.requestedCheckInTime) : "-"}<br>${x.requestedCheckOutTime ? formatDateTime(x.requestedCheckOutTime) : "-"}</td>
          <td><span class="status-pill ${employeeStatusClass(x.status)}">${escapeHtml(x.status)}</span></td>
          <td>${escapeHtml(x.reviewNote || (x.reviewedByUsername ? `Reviewed by ${x.reviewedByUsername}${x.reviewedAt ? ` on ${formatDateTime(x.reviewedAt)}` : ""}` : "Pending review"))}</td>
          <td><button class="btn btn-sm btn-outline-primary" data-adjustment-history-id="${x.id}">History</button></td>
        </tr>`).join("")
      : `<tr><td colspan="7" class="text-center text-muted py-4">No adjustment requests yet</td></tr>`;

    if (adjustmentStats) {
      const pending = items.filter(x => (x.status || "").toLowerCase() === "pending").length;
      const approved = items.filter(x => (x.status || "").toLowerCase() === "approved").length;
      const rejected = items.filter(x => (x.status || "").toLowerCase() === "rejected").length;
      adjustmentStats.innerHTML = `
        <span class="status-chip status-pending">Pending ${pending}</span>
        <span class="status-chip status-approved">Approved ${approved}</span>
        <span class="status-chip status-rejected">Rejected ${rejected}</span>`;
    }

    requestBody.querySelectorAll("[data-adjustment-history-id]").forEach(btn => {
      btn.addEventListener("click", async () => {
        selectedAdjustmentId = btn.dataset.adjustmentHistoryId;
        renderAdjustmentRequests();
        await renderAdjustmentHistory(selectedAdjustmentId);
      });
    });

    renderPager(adjustmentPaging, paging.adjustments, items.length, 'adjustment request(s)', renderAdjustmentRequests);
  }

  function renderAllVisuals() {
    renderAttendanceDashboard();
    renderAttendances();
    renderAdjustmentRequests();
  }

  async function syncHistorySelection() {
    const visibleItems = getFilteredAdjustments();
    if (!visibleItems.some(x => String(x.id) === String(selectedAdjustmentId))) {
      selectedAdjustmentId = visibleItems[0]?.id ?? null;
    }
    renderAdjustmentRequests();
    await renderAdjustmentHistory(selectedAdjustmentId);
  }

  async function fetchAndRender() {
    const params = new URLSearchParams();
    if (monthInput.value) params.set("month", monthInput.value);
    if (yearInput.value) params.set("year", yearInput.value);
    if (workDateInput.value) params.set("workDate", workDateInput.value);
    if (statusInput.value) params.set("status", statusInput.value);

    const requestParams = new URLSearchParams();
    if (myAdjustmentMonthFilter?.value) requestParams.set("month", myAdjustmentMonthFilter.value);
    if (myAdjustmentYearFilter?.value) requestParams.set("year", myAdjustmentYearFilter.value);
    if (myAdjustmentStatusFilter?.value) requestParams.set("status", myAdjustmentStatusFilter.value);

    const [data, requests] = await Promise.all([
      apiGet(`/api/Attendances/my-attendances?${params.toString()}`),
      apiGet(`/api/Attendances/my-adjustment-requests?${requestParams.toString()}`)
    ]);

    attendanceCache = Array.isArray(data) ? data : [];
    adjustmentCache = Array.isArray(requests) ? requests : [];
    paging.attendances.page = 1;
    paging.adjustments.page = 1;
    renderAttendanceDashboard();
    renderAttendances();
    await syncHistorySelection();
  }

  filterBtn?.addEventListener("click", fetchAndRender);
  resetBtn?.addEventListener("click", () => {
    monthInput.value = now.getMonth() + 1;
    yearInput.value = now.getFullYear();
    workDateInput.value = "";
    statusInput.value = "";
    if (searchInput) searchInput.value = "";
    if (pageSizeInput) {
      pageSizeInput.value = '8';
      paging.attendances.pageSize = 8;
    }
    fetchAndRender();
  });

  searchInput?.addEventListener('input', () => {
    paging.attendances.page = 1;
    renderAttendanceDashboard();
    renderAttendances();
  });
  pageSizeInput?.addEventListener('change', () => {
    paging.attendances.pageSize = Number(pageSizeInput.value || 8);
    paging.attendances.page = 1;
    renderAttendances();
  });

  exportCsvBtn?.addEventListener('click', () => {
    const rows = getFilteredAttendances().map(x => [
      formatDate(x.workDate),
      formatTime(x.checkInTime),
      formatTime(x.checkOutTime),
      x.status,
      x.workingHours ?? '-',
      x.note || '-',
      x.sourceType || '-'
    ]);
    employeeExportCsv(`my-attendances-${yearInput.value || 'all'}-${monthInput.value || 'all'}.csv`, ['Date', 'Check In', 'Check Out', 'Status', 'Working Hours', 'Note', 'Source'], rows);
  });
  exportPrintBtn?.addEventListener('click', () => {
    const rows = getFilteredAttendances().map(x => [
      formatDate(x.workDate),
      formatTime(x.checkInTime),
      formatTime(x.checkOutTime),
      x.status,
      String(x.workingHours ?? '-'),
      x.note || '-'
    ]);
    employeeOpenPrintReport('My Attendance Records', 'Filtered attendance snapshot from the employee self-service portal.', ['Date', 'Check In', 'Check Out', 'Status', 'Working Hours', 'Note'], rows);
  });

  myAdjustmentApplyBtn?.addEventListener("click", fetchAndRender);
  myAdjustmentResetBtn?.addEventListener("click", () => {
    if (myAdjustmentStatusFilter) myAdjustmentStatusFilter.value = "";
    if (myAdjustmentMonthFilter) myAdjustmentMonthFilter.value = now.getMonth() + 1;
    if (myAdjustmentYearFilter) myAdjustmentYearFilter.value = now.getFullYear();
    if (adjustmentSearchInput) adjustmentSearchInput.value = '';
    if (adjustmentPageSizeInput) {
      adjustmentPageSizeInput.value = '6';
      paging.adjustments.pageSize = 6;
    }
    fetchAndRender();
  });

  adjustmentSearchInput?.addEventListener('input', async () => {
    paging.adjustments.page = 1;
    renderAttendanceDashboard();
    await syncHistorySelection();
  });
  adjustmentPageSizeInput?.addEventListener('change', async () => {
    paging.adjustments.pageSize = Number(adjustmentPageSizeInput.value || 6);
    paging.adjustments.page = 1;
    await syncHistorySelection();
  });

  adjustmentExportCsvBtn?.addEventListener('click', () => {
    const rows = getFilteredAdjustments().map(x => [
      formatDateTime(x.createdAt),
      formatDate(x.workDate),
      x.requestedStatus,
      x.requestedCheckInTime ? formatDateTime(x.requestedCheckInTime) : '-',
      x.requestedCheckOutTime ? formatDateTime(x.requestedCheckOutTime) : '-',
      x.status,
      x.reviewNote || '-',
      x.reviewedByUsername || '-',
      x.reviewedAt ? formatDateTime(x.reviewedAt) : '-',
      x.reason || '-'
    ]);
    employeeExportCsv(`attendance-adjustments-${myAdjustmentYearFilter?.value || 'all'}-${myAdjustmentMonthFilter?.value || 'all'}.csv`, ['Submitted', 'Work Date', 'Requested Status', 'Requested Check In', 'Requested Check Out', 'Status', 'Review Note', 'Reviewed By', 'Reviewed At', 'Reason'], rows);
  });
  adjustmentExportPrintBtn?.addEventListener('click', () => {
    const rows = getFilteredAdjustments().map(x => [
      formatDateTime(x.createdAt),
      formatDate(x.workDate),
      x.requestedStatus,
      x.status,
      x.reviewedByUsername || '-',
      x.reason || '-'
    ]);
    employeeOpenPrintReport('Attendance Adjustment Requests', 'Filtered correction-request queue from the employee self-service portal.', ['Submitted', 'Work Date', 'Requested Status', 'Status', 'Reviewed By', 'Reason'], rows);
  });

  document.getElementById("adjustmentWorkDateInput")?.addEventListener("change", syncAdjustmentDateTimeDefaults);
  document.getElementById("adjustmentStatusInput")?.addEventListener("change", syncAdjustmentDateTimeDefaults);
  syncAdjustmentDateTimeDefaults();

  adjustmentForm?.addEventListener("submit", async (e) => {
    e.preventDefault();
    adjustmentMessage.textContent = "";

    const payload = {
      workDate: document.getElementById("adjustmentWorkDateInput").value,
      requestedStatus: document.getElementById("adjustmentStatusInput").value,
      requestedCheckInTime: document.getElementById("adjustmentCheckInInput").value || null,
      requestedCheckOutTime: document.getElementById("adjustmentCheckOutInput").value || null,
      reason: document.getElementById("adjustmentReasonInput").value.trim()
    };

    try {
      await apiJson("/api/Attendances/adjustment-requests", "POST", payload);
      adjustmentMessage.className = "mt-3 small text-success";
      adjustmentMessage.textContent = "Attendance adjustment request submitted successfully.";
      adjustmentForm.reset();
      syncAdjustmentDateTimeDefaults();
      await fetchAndRender();
    } catch (err) {
      adjustmentMessage.className = "mt-3 small text-danger";
      adjustmentMessage.textContent = err.message;
    }
  });

  await fetchAndRender();
}

async function loadPayrollsPage() {
  const monthInput = document.getElementById("payrollMonth");
  const yearInput = document.getElementById("payrollYear");
  const searchInput = document.getElementById("payrollSearchInput");
  const pageSizeInput = document.getElementById("payrollPageSize");
  const filterBtn = document.getElementById("payrollFilterBtn");
  const resetBtn = document.getElementById("payrollResetBtn");
  const exportCsvBtn = document.getElementById("payrollExportCsvBtn");
  const exportPrintBtn = document.getElementById("payrollExportPrintBtn");
  const body = document.getElementById("payrollTableBody");
  const pagingHost = document.getElementById("employeePayrollPaging");
  const statsHost = document.getElementById("employeePayrollStats");
  const now = new Date();
  const paging = { page: 1, pageSize: Number(pageSizeInput?.value || 8) };
  let payrollItems = [];
  monthInput.value = now.getMonth() + 1;
  yearInput.value = now.getFullYear();

  function filteredItems() {
    const keyword = String(searchInput?.value || '').trim().toLowerCase();
    return payrollItems.filter(item => {
      const hay = [
        `${item.payrollMonth}/${item.payrollYear}`,
        item.baseSalary,
        item.bonus,
        item.deduction,
        item.netSalary,
        item.presentDays,
        item.leaveDays
      ].join(' ').toLowerCase();
      return !keyword || hay.includes(keyword);
    });
  }

  function renderStats(items) {
    if (statsHost) {
      const totalNet = items.reduce((sum, item) => sum + Number(item.netSalary || 0), 0);
      const highestNet = items.reduce((max, item) => Math.max(max, Number(item.netSalary || 0)), 0);
      statsHost.innerHTML = `
        <span class="status-chip status-approved">Records: ${items.length}</span>
        <span class="status-chip status-pending">Total Net: ${employeeMoney(totalNet)}</span>
        <span class="status-chip status-cancelled">Highest Net: ${employeeMoney(highestNet)}</span>`;
      document.getElementById('payrollKpiRecords').textContent = items.length;
      document.getElementById('payrollKpiTotalNet').textContent = employeeMoney(totalNet);
      document.getElementById('payrollKpiAverageNet').textContent = employeeMoney(items.length ? totalNet / items.length : 0);
      document.getElementById('payrollKpiHighestNet').textContent = employeeMoney(highestNet);
      document.getElementById('payrollKpiFocus').textContent = monthInput.value && yearInput.value ? `Focused on ${String(monthInput.value).padStart(2, '0')}/${yearInput.value}` : 'Cross-period payroll view';
    }

    const highlightHost = document.getElementById('employeePayrollHighlights');
    if (highlightHost) {
      const sorted = [...items].sort((a, b) => (b.payrollYear * 100 + b.payrollMonth) - (a.payrollYear * 100 + a.payrollMonth));
      const latest = sorted[0];
      const best = [...items].sort((a, b) => Number(b.netSalary || 0) - Number(a.netSalary || 0))[0];
      const totalBonus = items.reduce((sum, item) => sum + Number(item.bonus || 0), 0);
      const totalDeduction = items.reduce((sum, item) => sum + Number(item.deduction || 0), 0);
      const cards = [
        latest ? { title: 'Latest payroll snapshot', text: `${latest.payrollMonth}/${latest.payrollYear} closed with net salary ${employeeMoney(latest.netSalary)} and ${latest.presentDays ?? 0} present days.` } : null,
        best ? { title: 'Best take-home period', text: `${best.payrollMonth}/${best.payrollYear} is your highest filtered net salary at ${employeeMoney(best.netSalary)}.` } : null,
        { title: 'Compensation balance', text: `Across the current view, total bonus is ${employeeMoney(totalBonus)} while total deduction is ${employeeMoney(totalDeduction)}.` }
      ].filter(Boolean);
      highlightHost.innerHTML = cards.map(card => `<div class="employee-highlight-card"><strong>${escapeHtml(card.title)}</strong><span>${escapeHtml(card.text)}</span></div>`).join('');
    }

    renderEmployeeChart('employeePayrollTrend', 'employeePayrollTrendChart', {
      type: 'line',
      data: {
        labels: items.map(item => `${item.payrollMonth}/${item.payrollYear}`),
        datasets: [{ label: 'Net salary', data: items.map(item => Number(item.netSalary || 0)), borderColor: 'rgba(37,99,235,.95)', backgroundColor: 'rgba(37,99,235,.18)', fill: true, tension: 0.3, borderWidth: 3, pointRadius: 4 }]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true } } }
    });
    renderEmployeeChart('employeePayrollComposition', 'employeePayrollCompositionChart', {
      type: 'doughnut',
      data: {
        labels: ['Base salary total', 'Bonus total', 'Deduction total'],
        datasets: [{ data: [items.reduce((sum, item) => sum + Number(item.baseSalary || 0), 0), items.reduce((sum, item) => sum + Number(item.bonus || 0), 0), items.reduce((sum, item) => sum + Number(item.deduction || 0), 0)], backgroundColor: ['rgba(37,99,235,.82)','rgba(16,185,129,.82)','rgba(239,68,68,.82)'], borderWidth: 0 }]
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } } }
    });
  }

  function renderPaging(totalItems) {
    if (!pagingHost) return;
    const totalPages = Math.max(1, Math.ceil(totalItems / paging.pageSize));
    if (paging.page > totalPages) paging.page = totalPages;
    pagingHost.innerHTML = `<div class="employee-page-bar"><small class="text-muted">Page ${paging.page} of ${totalPages} · ${totalItems} payroll record(s)</small><div class="d-flex gap-2 flex-wrap"><button class="btn btn-sm btn-outline-secondary" data-payroll-page="first" ${paging.page === 1 ? 'disabled' : ''}>First</button><button class="btn btn-sm btn-outline-secondary" data-payroll-page="prev" ${paging.page === 1 ? 'disabled' : ''}>Prev</button><button class="btn btn-sm btn-outline-secondary" data-payroll-page="next" ${paging.page === totalPages ? 'disabled' : ''}>Next</button><button class="btn btn-sm btn-outline-secondary" data-payroll-page="last" ${paging.page === totalPages ? 'disabled' : ''}>Last</button></div></div>`;
    pagingHost.querySelectorAll('[data-payroll-page]').forEach(btn => btn.addEventListener('click', () => {
      const action = btn.getAttribute('data-payroll-page');
      if (action === 'first') paging.page = 1;
      if (action === 'prev') paging.page = Math.max(1, paging.page - 1);
      if (action === 'next') paging.page = Math.min(totalPages, paging.page + 1);
      if (action === 'last') paging.page = totalPages;
      renderTable();
    }));
  }

  function renderTable() {
    const items = filteredItems().sort((a, b) => (b.payrollYear * 100 + b.payrollMonth) - (a.payrollYear * 100 + a.payrollMonth));
    renderStats(items);
    const start = (paging.page - 1) * paging.pageSize;
    const pageItems = items.slice(start, start + paging.pageSize);
    body.innerHTML = pageItems.length ? pageItems.map(x => `
      <tr>
        <td>${x.payrollMonth}/${x.payrollYear}</td>
        <td>${employeeMoney(x.baseSalary)}</td>
        <td>${employeeMoney(x.bonus)}</td>
        <td>${employeeMoney(x.deduction)}</td>
        <td>${employeeMoney(x.netSalary)}</td>
        <td>${x.presentDays ?? '-'} / ${x.effectiveWorkingDays ?? '-'}</td>
        <td>${x.leaveDays ?? '-'}</td>
      </tr>`).join('') : `<tr><td colspan="7" class="text-center text-muted py-4">No payroll records found</td></tr>`;
    renderPaging(items.length);
  }

  async function fetchAndRender() {
    const params = new URLSearchParams();
    if (monthInput.value) params.append('month', monthInput.value);
    if (yearInput.value) params.append('year', yearInput.value);
    payrollItems = await apiGet(`/api/Payrolls/my-payrolls?${params.toString()}`);
    paging.page = 1;
    renderTable();
  }

  exportCsvBtn?.addEventListener('click', () => {
    const rows = filteredItems().map(x => [
      `${x.payrollMonth}/${x.payrollYear}`,
      x.baseSalary,
      x.bonus,
      x.deduction,
      x.netSalary,
      x.presentDays,
      x.leaveDays,
      x.generatedAt ? formatDateTime(x.generatedAt) : '-'
    ]);
    employeeExportCsv(`my-payrolls-${yearInput.value || 'all'}-${monthInput.value || 'all'}.csv`, ['Month', 'Base Salary', 'Bonus', 'Deduction', 'Net Salary', 'Present Days', 'Leave Days', 'Generated At'], rows);
  });
  exportPrintBtn?.addEventListener('click', () => {
    const rows = filteredItems().map(x => [
      `${x.payrollMonth}/${x.payrollYear}`,
      employeeMoney(x.baseSalary),
      employeeMoney(x.bonus),
      employeeMoney(x.deduction),
      employeeMoney(x.netSalary),
      `${x.presentDays ?? '-'} / ${x.effectiveWorkingDays ?? '-'}`,
      `${x.leaveDays ?? '-'}`
    ]);
    employeeOpenPrintReport('My Payrolls', `Filtered payroll view generated on ${formatDateTime(new Date())}`, ['Month', 'Base Salary', 'Bonus', 'Deduction', 'Net Salary', 'Presence', 'Leave Days'], rows);
  });

  filterBtn?.addEventListener('click', fetchAndRender);
  resetBtn?.addEventListener('click', () => {
    monthInput.value = '';
    yearInput.value = '';
    if (searchInput) searchInput.value = '';
    if (pageSizeInput) pageSizeInput.value = '8';
    paging.pageSize = 8;
    fetchAndRender();
  });
  searchInput?.addEventListener('input', () => { paging.page = 1; renderTable(); });
  pageSizeInput?.addEventListener('change', () => { paging.pageSize = Number(pageSizeInput.value || 8); paging.page = 1; renderTable(); });

  await fetchAndRender();
}


async function loadLeavesPage() {
  const statusFilter = document.getElementById("leaveStatusFilter");
  const typeFilter = document.getElementById("leaveTypeFilter");
  const monthFilter = document.getElementById("leaveMonthFilter");
  const yearFilter = document.getElementById("leaveYearFilter");
  const searchInput = document.getElementById("leaveSearchInput");
  const pageSizeInput = document.getElementById("leavePageSize");
  const filterBtn = document.getElementById("leaveFilterBtn");
  const resetBtn = document.getElementById("leaveResetBtn");
  const exportCsvBtn = document.getElementById("leaveExportCsvBtn");
  const exportPrintBtn = document.getElementById("leaveExportPrintBtn");
  const form = document.getElementById("leaveCreateForm");
  const createMsg = document.getElementById("leaveCreateMessage");
  const body = document.getElementById("leaveTableBody");
  const statsHost = document.getElementById("employeeLeaveStats");
  const pagingHost = document.getElementById("employeeLeavePaging");
  const historyPanel = document.getElementById("employeeLeaveHistoryPanel");
  const paging = { page: 1, pageSize: Number(pageSizeInput?.value || 6) };
  let leaveItems = [];
  let selectedLeaveId = null;
  const today = new Date();
  if (monthFilter) monthFilter.value = today.getMonth() + 1;
  if (yearFilter) yearFilter.value = today.getFullYear();

  function filteredItems() {
    const keyword = String(searchInput?.value || '').trim().toLowerCase();
    return leaveItems.filter(item => {
      const statusOk = !statusFilter.value || String(item.status || '').toLowerCase() === String(statusFilter.value).toLowerCase();
      const typeOk = !typeFilter.value || String(item.leaveType || '').toLowerCase() === String(typeFilter.value).toLowerCase();
      const monthOk = !monthFilter.value || new Date(item.startDate).getMonth() + 1 === Number(monthFilter.value) || new Date(item.endDate).getMonth() + 1 === Number(monthFilter.value);
      const yearOk = !yearFilter.value || new Date(item.startDate).getFullYear() === Number(yearFilter.value) || new Date(item.endDate).getFullYear() === Number(yearFilter.value);
      const hay = [item.leaveType, item.reason, item.status, item.approvedByUsername, item.approvalNote, item.rejectionReason, formatDate(item.startDate), formatDate(item.endDate)].join(' ').toLowerCase();
      return statusOk && typeOk && monthOk && yearOk && (!keyword || hay.includes(keyword));
    });
  }

  function renderStats(items) {
    if (!statsHost) return;
    const counts = {
      pending: items.filter(x => String(x.status || '').toLowerCase() === 'pending').length,
      approved: items.filter(x => String(x.status || '').toLowerCase() === 'approved').length,
      rejected: items.filter(x => String(x.status || '').toLowerCase() === 'rejected').length,
      cancelled: items.filter(x => String(x.status || '').toLowerCase() === 'cancelled').length
    };
    statsHost.innerHTML = `
      <span class="status-chip status-pending">Pending: ${counts.pending}</span>
      <span class="status-chip status-approved">Approved: ${counts.approved}</span>
      <span class="status-chip status-rejected">Rejected: ${counts.rejected}</span>
      <span class="status-chip status-cancelled">Cancelled: ${counts.cancelled}</span>`;

    const approvedDays = items.filter(x => String(x.status || '').toLowerCase() === 'approved').reduce((sum, item) => sum + Number(item.totalDays || 0), 0);
    const filteredDays = items.reduce((sum, item) => sum + Number(item.totalDays || 0), 0);
    document.getElementById('leaveKpiTotal').textContent = items.length;
    document.getElementById('leaveKpiApprovedDays').textContent = approvedDays;
    document.getElementById('leaveKpiPending').textContent = counts.pending;
    document.getElementById('leaveKpiFilteredDays').textContent = filteredDays;
    document.getElementById('leaveKpiFocus').textContent = monthFilter?.value && yearFilter?.value ? `Focused on ${String(monthFilter.value).padStart(2, '0')}/${yearFilter.value}` : 'Cross-period leave workflow';

    const typeCounts = {
      AnnualLeave: items.filter(x => x.leaveType === 'AnnualLeave').length,
      SickLeave: items.filter(x => x.leaveType === 'SickLeave').length,
      UnpaidLeave: items.filter(x => x.leaveType === 'UnpaidLeave').length,
      Other: items.filter(x => x.leaveType === 'Other').length
    };
    renderEmployeeChart('employeeLeaveStatus', 'employeeLeaveStatusChart', {
      type: 'doughnut',
      data: { labels: ['Pending', 'Approved', 'Rejected', 'Cancelled'], datasets: [{ data: [counts.pending, counts.approved, counts.rejected, counts.cancelled], backgroundColor: ['rgba(245,158,11,.82)','rgba(16,185,129,.82)','rgba(239,68,68,.82)','rgba(100,116,139,.82)'], borderWidth: 0 }] },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } } }
    });
    renderEmployeeChart('employeeLeaveType', 'employeeLeaveTypeChart', {
      type: 'bar',
      data: { labels: ['Annual', 'Sick', 'Unpaid', 'Other'], datasets: [{ label: 'Requests', data: [typeCounts.AnnualLeave, typeCounts.SickLeave, typeCounts.UnpaidLeave, typeCounts.Other], backgroundColor: ['rgba(37,99,235,.82)','rgba(16,185,129,.82)','rgba(139,92,246,.82)','rgba(245,158,11,.82)'], borderRadius: 12, maxBarThickness: 46 }] },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
    });
    const highlightHost = document.getElementById('employeeLeaveHighlights');
    if (highlightHost) {
      const latestPending = items.find(x => String(x.status || '').toLowerCase() === 'pending');
      const latestApproved = items.find(x => String(x.status || '').toLowerCase() === 'approved');
      const cards = [
        latestPending ? { title: 'Pending workflow', text: `${latestPending.leaveType} from ${formatDate(latestPending.startDate)} to ${formatDate(latestPending.endDate)} is still waiting for review.` } : { title: 'Pending workflow', text: 'No pending leave request appears in the current filtered view.' },
        latestApproved ? { title: 'Latest approval', text: `${latestApproved.leaveType} was approved for ${latestApproved.totalDays ?? 0} day(s)${latestApproved.approvedByUsername ? ` by ${latestApproved.approvedByUsername}` : ''}.` } : { title: 'Latest approval', text: 'No approved leave request appears in the current filtered view.' },
        { title: 'Leave balance story', text: `${approvedDays} approved leave day(s) are visible across ${items.length} filtered request(s).` }
      ];
      highlightHost.innerHTML = cards.map(card => `<div class="employee-highlight-card"><strong>${escapeHtml(card.title)}</strong><span>${escapeHtml(card.text)}</span></div>`).join('');
    }
  }

  function renderPaging(totalItems) {
    if (!pagingHost) return;
    const totalPages = Math.max(1, Math.ceil(totalItems / paging.pageSize));
    if (paging.page > totalPages) paging.page = totalPages;
    pagingHost.innerHTML = `
      <div class="employee-page-bar">
        <small class="text-muted">Page ${paging.page} of ${totalPages} · ${totalItems} request(s)</small>
        <div class="d-flex gap-2 flex-wrap">
          <button class="btn btn-sm btn-outline-secondary" data-leave-page="first" ${paging.page === 1 ? 'disabled' : ''}>First</button>
          <button class="btn btn-sm btn-outline-secondary" data-leave-page="prev" ${paging.page === 1 ? 'disabled' : ''}>Prev</button>
          <button class="btn btn-sm btn-outline-secondary" data-leave-page="next" ${paging.page === totalPages ? 'disabled' : ''}>Next</button>
          <button class="btn btn-sm btn-outline-secondary" data-leave-page="last" ${paging.page === totalPages ? 'disabled' : ''}>Last</button>
        </div>
      </div>`;
    pagingHost.querySelectorAll('[data-leave-page]').forEach(btn => {
      btn.addEventListener('click', () => {
        const action = btn.getAttribute('data-leave-page');
        if (action === 'first') paging.page = 1;
        if (action === 'prev') paging.page = Math.max(1, paging.page - 1);
        if (action === 'next') paging.page = Math.min(totalPages, paging.page + 1);
        if (action === 'last') paging.page = totalPages;
        renderTable();
      });
    });
  }

  async function renderHistory(leaveId) {
    if (!historyPanel) return;
    if (!leaveId) {
      historyPanel.innerHTML = `<div class="empty-state">Select a leave request to inspect its workflow history and timeline.</div>`;
      return;
    }

    const activeList = filteredItems();
    const item = activeList.find(x => String(x.id) === String(leaveId)) || leaveItems.find(x => String(x.id) === String(leaveId));
    if (!item) {
      historyPanel.innerHTML = `<div class="empty-state">The selected leave request is not visible in the current filtered list.</div>`;
      return;
    }

    const history = await fetchEmployeeLeaveHistory(leaveId);
    const currentState = history[0]?.newStatus || item.status || '-';
    const timelineHtml = history.length
      ? history.map(entry => `
        <div class="employee-timeline-item compact">
          <div class="employee-timeline-icon"><i class="bi bi-journal-check"></i></div>
          <div class="employee-timeline-copy">
            <strong>${escapeHtml(entry.actionType || 'Workflow update')}</strong>
            <span>${escapeHtml(`${entry.performedByUsername || 'System'} · ${formatDateTime(entry.createdAt)}`)}</span>
            <small>${escapeHtml(`${entry.previousStatus || '-'} → ${entry.newStatus || '-'}${entry.note ? ` · ${entry.note}` : ''}`)}</small>
          </div>
        </div>`).join('')
      : `<div class="empty-state">No leave audit history is available for this request yet.</div>`;

    historyPanel.innerHTML = `
      <div class="employee-leave-history-header">
        <div>
          <h4 class="sub-card-title mb-1">Leave History & Timeline</h4>
          <p class="text-muted mb-0">Selected request: ${escapeHtml(item.leaveType || '-')} · ${formatDate(item.startDate)} → ${formatDate(item.endDate)}</p>
        </div>
        <span class="status-pill ${employeeStatusClass(currentState)}">${escapeHtml(currentState)}</span>
      </div>
      <div class="history-meta-grid">
        <div class="history-meta-card"><span>Total Days</span><strong>${item.totalDays ?? '-'}</strong></div>
        <div class="history-meta-card"><span>Submitted</span><strong>${formatDateTime(item.createdAt)}</strong></div>
        <div class="history-meta-card"><span>Reviewer</span><strong>${escapeHtml(item.approvedByUsername || 'Pending')}</strong></div>
      </div>
      <div class="employee-timeline-list">
        ${timelineHtml}
      </div>`;
  }

  function renderTable() {
    const data = filteredItems();
    renderStats(data);
    const totalPages = Math.max(1, Math.ceil(data.length / paging.pageSize));
    if (paging.page > totalPages) paging.page = totalPages;
    const start = (paging.page - 1) * paging.pageSize;
    const pageItems = data.slice(start, start + paging.pageSize);

    body.innerHTML = pageItems.length
      ? pageItems.map(x => {
          const canCancel = (x.status || "").toLowerCase() === "pending";
          const isSelected = String(selectedLeaveId) === String(x.id);
          return `
          <tr class="${isSelected ? 'employee-row-selected' : ''}">
            <td>${escapeHtml(x.leaveType)}</td>
            <td>${formatDate(x.startDate)} - ${formatDate(x.endDate)}</td>
            <td>${x.totalDays ?? "-"}</td>
            <td><span class="status-pill ${employeeStatusClass(x.status)}">${escapeHtml(x.status)}</span></td>
            <td>${escapeHtml(x.reason || "-")}</td>
            <td>
              <div class="d-flex gap-2 flex-wrap">
                <button class="btn btn-sm btn-outline-primary leave-history-btn" data-id="${x.id}"><i class="bi bi-clock-history"></i> History</button>
                ${canCancel ? `<button class="btn btn-sm btn-outline-danger leave-cancel-btn" data-id="${x.id}"><i class="bi bi-x-circle"></i> Cancel</button>` : `<span class="text-muted small align-self-center">No action</span>`}
              </div>
            </td>
          </tr>`;
        }).join("")
      : `<tr><td colspan="6" class="text-center text-muted py-4">No leave requests found</td></tr>`;

    renderPaging(data.length);

    body.querySelectorAll(".leave-cancel-btn").forEach(btn => {
      btn.addEventListener("click", async () => {
        const id = btn.getAttribute("data-id");
        if (!confirm("Cancel this pending leave request?")) return;

        try {
          await apiJson(`/api/LeaveRequests/${id}/cancel`, "PUT", {});
          await fetchAndRender();
          selectedLeaveId = id;
          await renderHistory(id);
        } catch (err) {
          alert(err.message);
        }
      });
    });

    body.querySelectorAll('.leave-history-btn').forEach(btn => {
      btn.addEventListener('click', async () => {
        selectedLeaveId = btn.getAttribute('data-id');
        renderTable();
        await renderHistory(selectedLeaveId);
      });
    });
  }

  async function fetchAndRender() {
    const params = new URLSearchParams();
    if (statusFilter.value) params.append('status', statusFilter.value);
    if (typeFilter.value) params.append('leaveType', typeFilter.value);
    if (monthFilter.value && yearFilter.value) {
      const first = `${yearFilter.value}-${String(monthFilter.value).padStart(2, '0')}-01`;
      const lastDay = new Date(Number(yearFilter.value), Number(monthFilter.value), 0).getDate();
      const last = `${yearFilter.value}-${String(monthFilter.value).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`;
      params.append('fromDate', first);
      params.append('toDate', last);
    }
    try {
      leaveItems = await apiGet(`/api/LeaveRequests/my-requests?${params.toString()}`);
    } catch (err) {
      console.error(err);
      leaveItems = [];
    }
    paging.page = 1;
    renderTable();
    if (selectedLeaveId) await renderHistory(selectedLeaveId); else await renderHistory(null);
  }

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    createMsg.textContent = "";
    try {
      await apiJson("/api/LeaveRequests", "POST", {
        leaveType: document.getElementById("leaveTypeInput").value,
        startDate: document.getElementById("leaveStartDateInput").value,
        endDate: document.getElementById("leaveEndDateInput").value,
        reason: document.getElementById("leaveReasonInput").value.trim()
      });
      createMsg.className = "mt-3 small text-success";
      createMsg.textContent = "Leave request submitted successfully.";
      form.reset();
      await fetchAndRender();
    } catch (err) {
      createMsg.className = "mt-3 small text-danger";
      createMsg.textContent = err.message;
    }
  });

  exportCsvBtn?.addEventListener('click', () => {
    const rows = filteredItems().map(x => [x.leaveType, `${formatDate(x.startDate)} → ${formatDate(x.endDate)}`, x.totalDays, x.status, x.reason || '-', x.approvedByUsername || '-', x.approvedAt ? formatDateTime(x.approvedAt) : '-']);
    employeeExportCsv(`my-leave-requests-${yearFilter.value || 'all'}-${monthFilter.value || 'all'}.csv`, ['Leave Type', 'Dates', 'Total Days', 'Status', 'Reason', 'Reviewer', 'Approved At'], rows);
  });
  exportPrintBtn?.addEventListener('click', () => {
    const rows = filteredItems().map(x => [x.leaveType, `${formatDate(x.startDate)} → ${formatDate(x.endDate)}`, `${x.totalDays ?? '-'}`, x.status, x.reason || '-']);
    employeeOpenPrintReport('My Leave Requests', `Filtered leave workflow generated on ${formatDateTime(new Date())}`, ['Leave Type', 'Dates', 'Total Days', 'Status', 'Reason'], rows);
  });

  filterBtn.addEventListener("click", () => {
    paging.page = 1;
    fetchAndRender();
  });
  resetBtn.addEventListener("click", () => {
    statusFilter.value = "";
    typeFilter.value = "";
    if (monthFilter) monthFilter.value = '';
    if (yearFilter) yearFilter.value = '';
    if (searchInput) searchInput.value = '';
    if (pageSizeInput) pageSizeInput.value = '6';
    paging.pageSize = 6;
    paging.page = 1;
    fetchAndRender();
  });
  searchInput?.addEventListener('input', () => { paging.page = 1; renderTable(); });
  pageSizeInput?.addEventListener('change', () => { paging.pageSize = Number(pageSizeInput.value || 6); paging.page = 1; renderTable(); });

  await fetchAndRender();
}


async function loadProfilePage() {
  const profile = await apiGet("/api/Employees/my-profile");

  document.getElementById("profileEmployeeCode").textContent = profile.employeeCode || "-";
  document.getElementById("profileFullName").textContent = profile.fullName || "-";
  document.getElementById("profileEmail").textContent = profile.email || "-";
  document.getElementById("profileDepartment").textContent = profile.departmentName || "-";
  document.getElementById("profilePosition").textContent = profile.positionName || "-";
  document.getElementById("profileStatus").textContent = profile.isActive ? "Active" : "Inactive";
  document.getElementById("profileFullNameInput").value = profile.fullName || "";
  document.getElementById("profileEmailInput").value = profile.email || "";

  const updateForm = document.getElementById("profileUpdateForm");
  const updateMsg = document.getElementById("profileUpdateMessage");

  updateForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    updateMsg.textContent = "";

    try {
      await apiJson("/api/Employees/my-profile", "PUT", {
        fullName: document.getElementById("profileFullNameInput").value.trim(),
        email: document.getElementById("profileEmailInput").value.trim()
      });

      updateMsg.className = "mt-3 small text-success";
      updateMsg.textContent = "Profile updated successfully.";
      await loadProfilePage();
    } catch (err) {
      updateMsg.className = "mt-3 small text-danger";
      updateMsg.textContent = err.message;
    }
  });

  const pwdForm = document.getElementById("changePasswordForm");
  const pwdMsg = document.getElementById("changePasswordMessage");

  pwdForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    pwdMsg.textContent = "";

    const currentPassword = document.getElementById("currentPasswordInput").value;
    const newPassword = document.getElementById("newPasswordInput").value;
    const confirmPassword = document.getElementById("confirmPasswordInput").value;

    if (newPassword !== confirmPassword) {
      pwdMsg.className = "mt-3 small text-danger";
      pwdMsg.textContent = "Confirm password does not match.";
      return;
    }

    try {
      await apiJson("/api/Auth/change-password", "POST", {
        currentPassword,
        newPassword,
        confirmNewPassword: confirmPassword
      });

      pwdMsg.className = "mt-3 small text-success";
      pwdMsg.textContent = "Password changed successfully.";
      pwdForm.reset();
    } catch (err) {
      pwdMsg.className = "mt-3 small text-danger";
      pwdMsg.textContent = err.message;
    }
  });
}

(async function bootstrapEmployeePortal() {
  const path = window.location.pathname.toLowerCase();

  if (path.includes("/employee/login.html")) {
    if (!ensureEmployeeGuard()) return;
    loadEmployeeLoginPage();
    return;
  }

  if (!ensureEmployeeGuard()) return;
  initEmployeeShell();

  try {
    if (path.includes("/employee/overview.html") || path.endsWith("/employee/portal.html")) {
      await loadOverviewPage();
    } else if (path.includes("/employee/attendances.html")) {
      await loadAttendancesPage();
    } else if (path.includes("/employee/payrolls.html")) {
      await loadPayrollsPage();
    } else if (path.includes("/employee/leaves.html")) {
      await loadLeavesPage();
    } else if (path.includes("/employee/profile.html")) {
      await loadProfilePage();
    }
  } catch (err) {
    console.error(err);
    alert("Failed to load employee portal data.");
  }
})();
