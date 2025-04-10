/**
 * Dynamic Page Loader with Lifecycle Management
 * - Loads HTML and JS on demand
 * - Maintains component lifecycle
 * - Auto-cleans resources
 */

const PageManager = {
    // Current page context
    current: null,
    
    // Container element
    container: null,
    filesContent: {},
    // Initialize the manager
    init(containerId) {
      receiveMessage(message => {
        console.log(message);
        if (message.indexOf("page_files") > -1) console.log(message);
        const data = JSON.parse(message);
        switch (data.type) {
          case "page_files":
            console.log(data);
            this.filesContent[data.pageName] = {
              html: data.html,
              js: data.js,
              css: data.css
            }
            this.drawPage(data.pageName);
          break;
        }
      });

      this.container = document.getElementById(containerId);
      if (!this.container) {
        throw new Error(`Container element #${containerId} not found`);
      }
      $(this.container).html('<div class="page-placeholder">Select a page to load</div>');
      return this;
    },
    
    // Load a page dynamically
    async loadPage(pageName) {
      window.external.sendMessage(JSON.stringify({
        action: "page_files",
        pageName: pageName
      }));
    },

    async drawPage(pageName) {
      $("#sidenav li.active").removeClass("active");
      $("#sidenav li[data-page=" + pageName + "]").addClass("active");

      // Clean up previous page if any
      await this.unloadCurrentPage();
      
      try {
        // Fetch HTML content
        const html = this.filesContent[pageName].html;
        
        // Render the HTML
        $(this.container).html(html);
        
        // Create a page context object
        const pageContext = {
          name: pageName,
          _events: [],
          _timers: [],
          
          // Helper methods for resource tracking
          addEventListener(element, event, handler) {
            $(element).on(event, handler);
            this._events.push({ element, event, handler });
          },
          
          removeEventListener(element, event, handler) {
            $(element).off(event, handler);
            const index = this._events.findIndex(e => 
              e.element === element && e.event === event && e.handler === handler
            );
            if (index !== -1) this._events.splice(index, 1);
          },
          
          setInterval(callback, delay) {
            const id = setInterval(callback, delay);
            this._timers.push({ type: 'interval', id });
            return id;
          },
          
          clearInterval(id) {
            clearInterval(id);
            const index = this._timers.findIndex(t => t.type === 'interval' && t.id === id);
            if (index !== -1) this._timers.splice(index, 1);
          },
          
          setTimeout(callback, delay) {
            const id = setTimeout(callback, delay);
            this._timers.push({ type: 'timeout', id });
            return id;
          },
          
          clearTimeout(id) {
            clearTimeout(id);
            const index = this._timers.findIndex(t => t.type === 'timeout' && t.id === id);
            if (index !== -1) this._timers.splice(index, 1);
          }
        };
        
        // Load and execute the page's JavaScript
        try {
          // Fetch JS module
          const jsCode = this.filesContent[pageName].js;
            
          // Create a function that will execute in the context of the page
          const pageModuleFunc = new Function('page', `
            // Code wrapper for page scripts
            return (function() {
              ${jsCode}
              
              // Return any exported lifecycle hooks
              return {
                init: typeof init === 'function' ? init : null,
                cleanup: typeof cleanup === 'function' ? cleanup : null
              };
            })();
          `);
          
          // Execute the page module with the page context
          const pageModule = pageModuleFunc(pageContext);
          
          // Store references to lifecycle hooks
          pageContext.initFunc = pageModule.init;
          pageContext.cleanupFunc = pageModule.cleanup;
          
          // Call init function if provided
          if (pageContext.initFunc) {
            await pageContext.initFunc.call(pageContext);
          }
        } catch (jsError) {
          console.error(`Error loading or executing JavaScript for ${pageName}:`, jsError);
        }

        // Load and execute the page's css
        try {
          const cssCode = this.filesContent[pageName].css;
          const styleElement = document.createElement('style');
          styleElement.textContent = cssCode;
          $(this.container).append(styleElement);
        } catch (cssError) {
          console.error(`Error loading CSS for ${pageName}:`, cssError);
        }
        
        // Store the current page
        this.current = pageContext;
        
      } catch (error) {
        $(this.container).html(`<div class="error">Error loading page: ${error.message}</div>`);
      }
    },
    
    // Unload the current page
    async unloadCurrentPage() {
      if (!this.current) return Promise.resolve();
      window.external.clearPageEvents();
      try {
        // Call cleanup function if provided
        if (this.current.cleanupFunc) {
          await this.current.cleanupFunc.call(this.current);
        }
        
        // Clean up event listeners
        this.current._events.forEach(({ element, event, handler }) => {
          $(element).off(event, handler);
        });
        
        // Clean up timers
        this.current._timers.forEach(timer => {
          if (timer.type === 'interval') {
            clearInterval(timer.id);
          } else if (timer.type === 'timeout') {
            clearTimeout(timer.id);
          }
        });
        
        // Clear the container
        $(this.container).empty();
        
        // Reset current page
        this.current = null;
        
        return true;
      } catch (error) {
        console.error('Error unloading page:', error);
        return false;
      }
    }
  };