(() => {
  document.querySelectorAll('[data-auto-dismiss]').forEach((el) => {
    setTimeout(() => {
      el.classList.add('is-hiding');
      setTimeout(() => el.remove(), 280);
    }, 4500);
  });

  const modalEl = document.getElementById('appModal');
  if (!modalEl) return;

  const modal = new bootstrap.Modal(modalEl);
  const content = document.getElementById('appModalContent');

  function parseValidation(form) {
    if (!window.jQuery || !$.validator || !$.validator.unobtrusive) return;
    const $form = $(form);
    $form.removeData('validator');
    $form.removeData('unobtrusiveValidation');
    $.validator.unobtrusive.parse($form);
  }

  async function openModal(url) {
    const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    if (!response.ok) {
      window.alert('Unable to open this form right now.');
      return;
    }
    content.innerHTML = await response.text();
    wireForm();
    modal.show();
  }

  function wireForm() {
    const form = content.querySelector('form[data-modal-form]');
    if (!form) return;

    parseValidation(form);

    form.addEventListener('submit', async (e) => {
      e.preventDefault();
      const response = await fetch(form.action, {
        method: 'POST',
        body: new FormData(form),
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });

      const contentType = response.headers.get('content-type') || '';
      if (contentType.includes('application/json')) {
        const data = await response.json();
        modal.hide();
        if (data.refreshTarget && data.refreshUrl) {
          const target = document.querySelector(data.refreshTarget);
          if (target) {
            const html = await (await fetch(data.refreshUrl)).text();
            target.innerHTML = html;
            return;
          }
        }
        if (data.refreshUrl) {
          window.location.href = data.refreshUrl;
          return;
        }
        window.location.reload();
        return;
      }

      content.innerHTML = await response.text();
      wireForm();
    });
  }

  document.addEventListener('click', (e) => {
    const trigger = e.target.closest('[data-modal-url]');
    if (!trigger) return;
    e.preventDefault();
    openModal(trigger.getAttribute('data-modal-url'));
  });
})();

(() => {
  const grid = document.querySelector('[data-application-grid]');
  if (!grid) return;

  const filters = document.querySelector('[data-application-grid-filters]');
  const rows = grid.querySelector('[data-grid-rows]');
  const footer = grid.querySelector('[data-grid-footer]');
  const summary = grid.querySelector('[data-grid-summary]');
  const empty = grid.querySelector('[data-grid-empty]');
  const error = grid.querySelector('[data-grid-error]');
  const previous = grid.querySelector('[data-grid-previous]');
  const next = grid.querySelector('[data-grid-next]');
  const sortButtons = [...grid.querySelectorAll('[data-grid-sort]')];
  const state = {
    page: Number(grid.dataset.page) || 1,
    pageSize: Number(grid.dataset.pageSize) || 10,
    sort: grid.dataset.sort || 'Updated',
    direction: grid.dataset.direction || 'Descending',
    total: 0
  };

  function filterValue(name) {
    return filters?.elements.namedItem(name)?.value || '';
  }

  function buildParameters() {
    const parameters = new URLSearchParams({
      page: String(state.page),
      pageSize: String(state.pageSize),
      sort: state.sort,
      direction: state.direction
    });
    const status = filterValue('status');
    const propertyId = filterValue('propertyId');
    if (status) parameters.set('status', status);
    if (propertyId) parameters.set('propertyId', propertyId);
    return parameters;
  }

  function appendCell(row, value, className) {
    const cell = document.createElement('td');
    cell.textContent = value;
    if (className) cell.className = className;
    row.appendChild(cell);
    return cell;
  }

  function renderRows(items) {
    rows.replaceChildren();
    items.forEach((item) => {
      const row = document.createElement('tr');
      appendCell(row, `#${item.id}`, 'row-title');
      appendCell(row, item.applicantName);
      appendCell(row, item.propertyName);
      appendCell(row, item.unitNumber);

      const statusCell = document.createElement('td');
      const badge = document.createElement('span');
      const statusKey = String(item.status || '').toLowerCase();
      badge.className = `status-badge status-${statusKey}`;
      badge.textContent = item.status === 'UnderReview' ? 'Under Review' : item.status;
      statusCell.appendChild(badge);
      row.appendChild(statusCell);
      appendCell(row, new Date(item.updatedAtUtc).toLocaleString());

      const actionsCell = document.createElement('td');
      const actions = document.createElement('div');
      actions.className = 'table-actions';
      const open = document.createElement('a');
      open.className = 'btn btn-sm btn-outline-primary';
      open.href = item.openUrl;
      open.textContent = 'Open';
      actions.appendChild(open);
      if (item.claimUrl) {
        const claim = document.createElement('form');
        claim.method = 'post';
        claim.action = item.claimUrl;
        claim.className = 'd-inline';
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
          const hidden = document.createElement('input');
          hidden.type = 'hidden';
          hidden.name = '__RequestVerificationToken';
          hidden.value = token.value;
          claim.appendChild(hidden);
        }
        const claimBtn = document.createElement('button');
        claimBtn.type = 'submit';
        claimBtn.className = 'btn btn-sm btn-primary';
        claimBtn.textContent = 'Claim';
        claim.appendChild(claimBtn);
        actions.appendChild(claim);
      }
      if (item.reviewUrl) {
        const review = document.createElement('button');
        review.type = 'button';
        review.className = 'btn btn-sm btn-primary';
        review.dataset.modalUrl = item.reviewUrl;
        review.textContent = 'Review';
        actions.appendChild(review);
      }
      if (item.releaseUrl) {
        const release = document.createElement('form');
        release.method = 'post';
        release.action = item.releaseUrl;
        release.className = 'd-inline';
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
          const hidden = document.createElement('input');
          hidden.type = 'hidden';
          hidden.name = '__RequestVerificationToken';
          hidden.value = token.value;
          release.appendChild(hidden);
        }
        const releaseBtn = document.createElement('button');
        releaseBtn.type = 'submit';
        releaseBtn.className = 'btn btn-sm btn-outline-secondary';
        releaseBtn.textContent = 'Release';
        release.appendChild(releaseBtn);
        actions.appendChild(release);
      }
      if (item.claimedByDisplayName && !item.reviewUrl && !item.claimUrl) {
        const claimed = document.createElement('span');
        claimed.className = 'text-muted small';
        claimed.textContent = `Claimed by ${item.claimedByDisplayName}`;
        actions.appendChild(claimed);
      }
      actionsCell.appendChild(actions);
      row.appendChild(actionsCell);
      rows.appendChild(row);
    });
  }

  function renderState(data) {
    state.page = data.page;
    state.pageSize = data.pageSize;
    state.total = data.filteredTotal;
    renderRows(data.rows);
    const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize));
    summary.textContent = `${state.total} application${state.total === 1 ? '' : 's'} · Page ${state.page} of ${totalPages}`;
    previous.disabled = state.page <= 1;
    next.disabled = state.page >= totalPages;
    footer.hidden = state.total === 0;
    empty.hidden = state.total !== 0;
    grid.querySelector('.table-responsive').hidden = state.total === 0;

    sortButtons.forEach((button) => {
      button.dataset.baseLabel ||= button.textContent.trim();
      const active = button.dataset.gridSort === state.sort;
      button.textContent = button.dataset.baseLabel + (active ? (state.direction === 'Ascending' ? ' ↑' : ' ↓') : '');
      button.closest('th').setAttribute('aria-sort', active ? (state.direction === 'Ascending' ? 'ascending' : 'descending') : 'none');
    });
  }

  async function load() {
    error.hidden = true;
    const parameters = buildParameters();
    try {
      const response = await fetch(`${grid.dataset.endpoint}?${parameters}`, {
        headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
      });
      if (!response.ok) throw new Error(`Grid request failed: ${response.status}`);
      renderState(await response.json());
      const address = new URL(window.location.href);
      address.search = parameters.toString();
      window.history.replaceState({}, '', address);
    } catch {
      error.hidden = false;
    }
  }

  filters?.addEventListener('submit', (event) => {
    event.preventDefault();
    state.page = 1;
    state.pageSize = Number(filterValue('pageSize')) || 10;
    load();
  });
  sortButtons.forEach((button) => button.addEventListener('click', () => {
    const selected = button.dataset.gridSort;
    if (state.sort === selected)
      state.direction = state.direction === 'Ascending' ? 'Descending' : 'Ascending';
    else {
      state.sort = selected;
      state.direction = 'Ascending';
    }
    state.page = 1;
    load();
  }));
  previous.addEventListener('click', () => { if (state.page > 1) { state.page--; load(); } });
  next.addEventListener('click', () => {
    if (state.page * state.pageSize < state.total) { state.page++; load(); }
  });

  load();
})();
