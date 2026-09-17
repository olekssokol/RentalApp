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
