(function initLandingPage() {
  const header = document.getElementById('siteHeader');
  const role = localStorage.getItem('role');
  const username = localStorage.getItem('username');
  const revealNodes = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window) {
    const revealObserver = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('reveal-in');
          revealObserver.unobserve(entry.target);
        }
      });
    }, { threshold: 0.14 });
    revealNodes.forEach((node) => revealObserver.observe(node));
  } else {
    revealNodes.forEach((node) => node.classList.add('reveal-in'));
  }
  const updateHeaderState = () => {
    if (!header) return;
    header.classList.toggle('scrolled', window.scrollY > 18);
  };
  updateHeaderState();
  window.addEventListener('scroll', updateHeaderState, { passive: true });
  const counters = document.querySelectorAll('[data-count]');
  if ('IntersectionObserver' in window && counters.length) {
    const counterObserver = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        const el = entry.target;
        const target = Number(el.getAttribute('data-count') || '0');
        let current = 0;
        const increment = Math.max(1, Math.ceil(target / 28));
        const timer = setInterval(() => {
          current += increment;
          if (current >= target) {
            current = target;
            clearInterval(timer);
          }
          el.textContent = current;
        }, 34);
        counterObserver.unobserve(el);
      });
    }, { threshold: 0.4 });
    counters.forEach((counter) => counterObserver.observe(counter));
  }
  const insights = [
    'Attendance compliance remains strong across core departments.',
    'Payroll readiness is high with only a small number of adjustments pending.',
    'Leave requests are visible early, which supports faster HR response cycles.',
    'Role-based workspaces help keep management and self-service journeys cleaner.'
  ];
  const rotatingInsight = document.getElementById('rotatingInsight');
  if (rotatingInsight) {
    let insightIndex = 0;
    setInterval(() => {
      insightIndex = (insightIndex + 1) % insights.length;
      rotatingInsight.style.opacity = '0';
      rotatingInsight.style.transform = 'translateY(8px)';
      setTimeout(() => {
        rotatingInsight.textContent = insights[insightIndex];
        rotatingInsight.style.opacity = '1';
        rotatingInsight.style.transform = 'translateY(0)';
      }, 180);
    }, 3600);
    rotatingInsight.style.transition = 'opacity .22s ease, transform .22s ease';
  }
  const roleHintText = document.getElementById('roleHintText');
  const navEmployeeBtn = document.getElementById('navEmployeeBtn');
  const navLoginBtn = document.getElementById('navLoginBtn');
  const heroLoginBtn = document.getElementById('heroLoginBtn');
  const heroAdminBtn = document.getElementById('heroAdminBtn');
  const heroEmployeeBtn = document.getElementById('heroEmployeeBtn');
  const ctaSharedLoginBtn = document.getElementById('ctaSharedLoginBtn');
  const ctaAdminBtn = document.getElementById('ctaAdminBtn');
  const ctaEmployeeBtn = document.getElementById('ctaEmployeeBtn');
  function setButton(button, href, html) {
    if (!button) return;
    button.href = href;
    button.innerHTML = html;
  }
  if (role === 'Employee') {
    setButton(navEmployeeBtn, '/employee/overview.html', '<i class="bi bi-person-workspace"></i> My Workspace');
    setButton(navLoginBtn, '/employee/overview.html', '<i class="bi bi-grid"></i> Employee Area');
    setButton(heroLoginBtn, '/employee/overview.html', '<i class="bi bi-grid"></i> Open My Workspace');
    setButton(heroEmployeeBtn, '/employee/overview.html', '<i class="bi bi-person-workspace"></i> Employee Portal');
    setButton(ctaSharedLoginBtn, '/employee/overview.html', '<i class="bi bi-grid"></i> Go to Employee Workspace');
    setButton(ctaEmployeeBtn, '/employee/overview.html', '<i class="bi bi-person-workspace"></i> Employee Portal');
    if (heroAdminBtn) heroAdminBtn.style.display = 'none';
    if (ctaAdminBtn) ctaAdminBtn.style.display = 'none';
    if (roleHintText) roleHintText.textContent = `Signed in as ${username || 'Employee'}. Continue directly into your self-service workspace.`;
  } else if (role === 'Admin' || role === 'HR' || role === 'Manager') {
    setButton(navLoginBtn, '/admin/overview.html', '<i class="bi bi-speedometer2"></i> Dashboard');
    setButton(heroLoginBtn, '/admin/overview.html', '<i class="bi bi-speedometer2"></i> Open Dashboard');
    setButton(heroAdminBtn, '/admin/overview.html', '<i class="bi bi-shield-lock-fill"></i> Admin / HR Dashboard');
    setButton(ctaSharedLoginBtn, '/admin/overview.html', '<i class="bi bi-speedometer2"></i> Go to Admin Dashboard');
    setButton(ctaAdminBtn, '/admin/overview.html', '<i class="bi bi-shield-lock-fill"></i> Admin / HR Dashboard');
    if (navEmployeeBtn) navEmployeeBtn.style.display = 'none';
    if (heroEmployeeBtn) heroEmployeeBtn.style.display = 'none';
    if (ctaEmployeeBtn) ctaEmployeeBtn.style.display = 'none';
    if (roleHintText) roleHintText.textContent = `Signed in as ${username || role}. Continue into the management dashboard for operations and analytics.`;
  } else if (roleHintText) {
    roleHintText.textContent = 'Not signed in yet. Use Login by Role for automatic redirection.';
  }
})();
