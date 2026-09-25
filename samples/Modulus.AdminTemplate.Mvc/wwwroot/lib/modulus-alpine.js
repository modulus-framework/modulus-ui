/**
 * Modulus Alpine.js Components
 * Reusable UI state management for MVC/Razor Pages
 */

export const ModulusAlpine = {
  /**
   * Modal component: handles visibility, form submission, validation
   */
  modal() {
    return {
      visible: false,
      title: 'Modal',
      loading: false,
      errors: {},

      open(title = 'Modal') {
        this.title = title;
        this.visible = true;
        this.errors = {};
        this.$nextTick(() => {
          document.body.style.overflow = 'hidden';
        });
      },

      close() {
        this.visible = false;
        this.loading = false;
        this.errors = {};
        this.$nextTick(() => {
          document.body.style.overflow = '';
        });
      },

      handleSubmit(callback) {
        this.loading = true;
        if (callback) {
          callback().finally(() => {
            this.loading = false;
          });
        }
      },

      setErrors(validationErrors = {}) {
        this.errors = validationErrors;
      }
    };
  },

  /**
   * Drawer component: side panel with close animation
   */
  drawer() {
    return {
      visible: false,
      title: 'Drawer',
      placement: 'right', // 'left' | 'right' | 'top' | 'bottom'
      width: '360px',

      open(title = 'Drawer', placement = 'right') {
        this.title = title;
        this.placement = placement;
        this.visible = true;
        this.$nextTick(() => {
          document.body.style.overflow = 'hidden';
        });
      },

      close() {
        this.visible = false;
        this.$nextTick(() => {
          document.body.style.overflow = '';
        });
      }
    };
  },

  /**
   * Dropdown component: position menu relative to trigger
   */
  dropdown() {
    return {
      visible: false,
      placement: 'bottom', // 'top' | 'bottom' | 'left' | 'right'

      toggle() {
        this.visible = !this.visible;
      },

      open() {
        this.visible = true;
      },

      close() {
        this.visible = false;
      },

      handleMenuClick(callback) {
        if (callback) callback();
        this.close();
      }
    };
  },

  /**
   * Tabs component: tab panel switching
   */
  tabs() {
    return {
      activeKey: '1',

      setActiveKey(key) {
        this.activeKey = key;
      },

      isActive(key) {
        return this.activeKey === key;
      }
    };
  },

  /**
   * Collapse component: expandable panels
   */
  collapse() {
    return {
      activeKeys: [],

      toggleKey(key) {
        const index = this.activeKeys.indexOf(key);
        if (index > -1) {
          this.activeKeys.splice(index, 1);
        } else {
          this.activeKeys.push(key);
        }
      },

      isActive(key) {
        return this.activeKeys.includes(key);
      }
    };
  },

  /**
   * Select component: dropdown with search/filter
   */
  select() {
    return {
      visible: false,
      search: '',
      selected: null,
      options: [],

      open() {
        this.visible = true;
      },

      close() {
        this.visible = false;
        this.search = '';
      },

      selectOption(option) {
        this.selected = option;
        this.close();
      },

      filteredOptions() {
        if (!this.search) return this.options;
        const term = this.search.toLowerCase();
        return this.options.filter(opt =>
          (opt.label || '').toLowerCase().includes(term)
        );
      }
    };
  },

  /**
   * Message/Toast component: temporary notifications
   */
  message() {
    return {
      messages: [],

      add(type, content, duration = 3000) {
        const id = Date.now();
        const msg = { id, type, content };
        this.messages.push(msg);

        if (duration > 0) {
          setTimeout(() => {
            this.remove(id);
          }, duration);
        }
      },

      success(content, duration) {
        this.add('success', content, duration);
      },

      error(content, duration) {
        this.add('error', content, duration);
      },

      info(content, duration) {
        this.add('info', content, duration);
      },

      warning(content, duration) {
        this.add('warning', content, duration);
      },

      remove(id) {
        this.messages = this.messages.filter(m => m.id !== id);
      },

      clear() {
        this.messages = [];
      }
    };
  },

  /**
   * Popconfirm component: confirmation dialog
   */
  popconfirm() {
    return {
      visible: false,
      title: 'Confirm?',
      description: '',
      okText: 'OK',
      cancelText: 'Cancel',
      onOk: null,
      onCancel: null,
      loading: false,

      show(title, description, onOk, onCancel) {
        this.title = title;
        this.description = description;
        this.onOk = onOk;
        this.onCancel = onCancel;
        this.visible = true;
      },

      handleOk() {
        this.loading = true;
        if (this.onOk) {
          Promise.resolve(this.onOk()).finally(() => {
            this.loading = false;
            this.visible = false;
          });
        } else {
          this.visible = false;
          this.loading = false;
        }
      },

      handleCancel() {
        if (this.onCancel) this.onCancel();
        this.visible = false;
      }
    };
  },

  /**
   * DatePicker component: date selection
   */
  datePicker() {
    return {
      visible: false,
      value: null,
      currentMonth: new Date(),

      open() {
        this.visible = true;
      },

      close() {
        this.visible = false;
      },

      selectDate(date) {
        this.value = date;
        this.close();
      },

      getDaysInMonth() {
        const year = this.currentMonth.getFullYear();
        const month = this.currentMonth.getMonth();
        const days = [];
        const date = new Date(year, month, 1);

        while (date.getMonth() === month) {
          days.push(new Date(date));
          date.setDate(date.getDate() + 1);
        }

        return days;
      },

      previousMonth() {
        this.currentMonth.setMonth(this.currentMonth.getMonth() - 1);
        this.$nextTick();
      },

      nextMonth() {
        this.currentMonth.setMonth(this.currentMonth.getMonth() + 1);
        this.$nextTick();
      }
    };
  },

  /**
   * Form validation state
   */
  formValidation() {
    return {
      errors: {},
      touched: {},

      setError(field, message) {
        if (!this.errors) this.errors = {};
        if (message) {
          this.errors[field] = message;
        } else {
          delete this.errors[field];
        }
      },

      touchField(field) {
        if (!this.touched) this.touched = {};
        this.touched[field] = true;
      },

      getError(field) {
        return (this.touched && this.touched[field]) ? this.errors?.[field] : null;
      },

      clearErrors() {
        this.errors = {};
        this.touched = {};
      }
    };
  }
};

// Global Alpine.js setup helper
export function initializeModulusAlpine() {
  if (typeof Alpine !== 'undefined') {
    Object.entries(ModulusAlpine).forEach(([name, factory]) => {
      Alpine.data(name, factory);
    });
  }
}

// Auto-initialize if Alpine is already loaded
if (typeof Alpine !== 'undefined') {
  initializeModulusAlpine();
}
