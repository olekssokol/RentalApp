(() => {
  document.querySelectorAll('[data-auto-dismiss]').forEach((el) => {
    setTimeout(() => {
      el.classList.add('is-hiding');
      setTimeout(() => el.remove(), 280);
    }, 4500);
  });
})();
