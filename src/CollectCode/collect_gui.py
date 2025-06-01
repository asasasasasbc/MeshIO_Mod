import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import os
import threading

def collect_cs_files_content_gui(root_dir, output_file, log_callback, progress_callback):
    """
    Recursively collects content from all .cs files in root_dir
    and writes it to output_file. Updates GUI via callbacks.

    Args:
        root_dir (str): The directory to start searching from.
        output_file (str): The path to the file where results will be saved.
        log_callback (function): Callback to send log messages to the GUI.
        progress_callback (function): Callback to update progress (0-100).
    """
    if not os.path.isdir(root_dir):
        log_callback(f"错误: 目录 '{root_dir}' 未找到.")
        progress_callback(-1) # Indicate error
        return

    cs_files_to_process = []
    for _, _, filenames in os.walk(root_dir):
        for filename in filenames:
            if filename.endswith(".cs"):
                cs_files_to_process.append(filename)
    
    total_files = len(cs_files_to_process)
    processed_files = 0

    if total_files == 0:
        log_callback(f"在 '{root_dir}' 中没有找到 .cs 文件。")
        progress_callback(100) # No files, so 100% done
        return

    try:
        with open(output_file, 'w', encoding='utf-8') as outfile:
            for dirpath, _, filenames in os.walk(root_dir):
                for filename in filenames:
                    if filename.endswith(".cs"):
                        processed_files += 1
                        progress_value = int((processed_files / total_files) * 100)
                        progress_callback(progress_value)

                        full_path = os.path.join(dirpath, filename)
                        relative_path = os.path.relpath(full_path, root_dir).replace(os.sep, '/')

                        header = f"{relative_path}-------------\n"
                        outfile.write(header)
                        log_callback(f"正在处理: {relative_path}")

                        try:
                            with open(full_path, 'r', encoding='utf-8-sig') as cs_file:
                                content = cs_file.read()
                                outfile.write(content)
                        except UnicodeDecodeError:
                            try:
                                with open(full_path, 'r', encoding='latin-1') as cs_file:
                                    content = cs_file.read()
                                    outfile.write(content)
                                outfile.write("\n[警告: 文件使用 latin-1 编码读取]\n")
                                log_callback(f"警告: {relative_path} 由于UTF-8解码错误，使用latin-1读取。")
                            except Exception as e_fallback:
                                error_msg = f"\n[错误: 无法读取文件 {relative_path} - {e_fallback}]\n"
                                outfile.write(error_msg)
                                log_callback(f"读取错误 {relative_path}: {e_fallback}")
                        except Exception as e:
                            error_msg = f"\n[错误: 无法处理文件 {relative_path} - {e}]\n"
                            outfile.write(error_msg)
                            log_callback(f"处理错误 {relative_path}: {e}")

                        outfile.write("\n\n")
        
        log_callback(f"\n成功将 .cs 文件内容收集到 '{output_file}'")
        progress_callback(100) # Ensure it hits 100% at the end
    except Exception as e:
        log_callback(f"写入输出文件时发生错误: {e}")
        progress_callback(-1) # Indicate error

class CsCollectorApp:
    def __init__(self, master):
        self.master = master
        master.title("C# 文件内容收集器")
        master.geometry("700x550")

        # --- Styling ---
        style = ttk.Style()
        style.configure("TButton", padding=5, font=('Helvetica', 10))
        style.configure("TLabel", padding=5, font=('Helvetica', 10))
        style.configure("TEntry", padding=5, font=('Helvetica', 10))

        # --- Frames ---
        input_frame = ttk.Frame(master, padding="10 10 10 10")
        input_frame.pack(fill=tk.X)

        action_frame = ttk.Frame(master, padding="10 0 10 10")
        action_frame.pack(fill=tk.X)
        
        log_frame = ttk.Frame(master, padding="10 0 10 10")
        log_frame.pack(fill=tk.BOTH, expand=True)

        # --- Input Directory ---
        ttk.Label(input_frame, text="源目录:").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        self.dir_entry_var = tk.StringVar(value=os.getcwd()) # Default to current dir
        self.dir_entry = ttk.Entry(input_frame, textvariable=self.dir_entry_var, width=60)
        self.dir_entry.grid(row=0, column=1, sticky=tk.EW, padx=5, pady=5)
        self.browse_dir_button = ttk.Button(input_frame, text="浏览...", command=self.browse_directory)
        self.browse_dir_button.grid(row=0, column=2, padx=5, pady=5)

        # --- Output File ---
        ttk.Label(input_frame, text="输出文件:").grid(row=1, column=0, sticky=tk.W, padx=5, pady=5)
        self.output_file_var = tk.StringVar(value=os.path.join(os.getcwd(), "result.txt")) # Default
        self.output_file_entry = ttk.Entry(input_frame, textvariable=self.output_file_var, width=60)
        self.output_file_entry.grid(row=1, column=1, sticky=tk.EW, padx=5, pady=5)
        self.browse_output_button = ttk.Button(input_frame, text="另存为...", command=self.browse_output_file)
        self.browse_output_button.grid(row=1, column=2, padx=5, pady=5)
        
        input_frame.columnconfigure(1, weight=1) # Make entry fields expandable

        # --- Action Button & Progress Bar ---
        self.start_button = ttk.Button(action_frame, text="开始收集", command=self.start_collection_thread)
        self.start_button.pack(side=tk.LEFT, padx=5, pady=5)

        self.progress_bar = ttk.Progressbar(action_frame, orient=tk.HORIZONTAL, length=300, mode='determinate')
        self.progress_bar.pack(side=tk.LEFT, padx=5, pady=5, fill=tk.X, expand=True)

        # --- Log Text Area ---
        ttk.Label(log_frame, text="日志:").pack(anchor=tk.W)
        self.log_text = tk.Text(log_frame, height=15, wrap=tk.WORD, font=('Courier New', 9))
        self.log_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, pady=(0,5))
        
        log_scrollbar = ttk.Scrollbar(log_frame, orient=tk.VERTICAL, command=self.log_text.yview)
        log_scrollbar.pack(side=tk.RIGHT, fill=tk.Y, pady=(0,5))
        self.log_text.config(yscrollcommand=log_scrollbar.set)


    def browse_directory(self):
        directory = filedialog.askdirectory()
        if directory:
            self.dir_entry_var.set(directory)
            # Update default output file based on new directory
            base_output_name = os.path.basename(self.output_file_var.get())
            if not base_output_name or base_output_name == ".txt": # if it was empty or just extension
                base_output_name = "result.txt"
            self.output_file_var.set(os.path.join(directory, base_output_name))


    def browse_output_file(self):
        filepath = filedialog.asksaveasfilename(
            defaultextension=".txt",
            filetypes=[("Text files", "*.txt"), ("All files", "*.*")],
            initialfile=os.path.basename(self.output_file_var.get() or "result.txt"),
            initialdir=os.path.dirname(self.output_file_var.get() or os.getcwd())
        )
        if filepath:
            self.output_file_var.set(filepath)

    def log_message(self, message):
        if self.master.winfo_exists(): # Check if window still exists
            self.log_text.insert(tk.END, message + "\n")
            self.log_text.see(tk.END) # Auto-scroll
            self.master.update_idletasks() # Force GUI update

    def update_progress(self, value):
        if self.master.winfo_exists():
            if value == -1: # Error occurred
                self.progress_bar['value'] = 0 # Reset or indicate error visually
                # You could change style here e.g. self.progress_bar.configure("Horizontal.TProgressbar", background='red')
            else:
                self.progress_bar['value'] = value
            self.master.update_idletasks()

    def start_collection_thread(self):
        root_dir = self.dir_entry_var.get()
        output_file = self.output_file_var.get()

        if not root_dir:
            messagebox.showerror("错误", "请输入源目录。")
            return
        if not os.path.isdir(root_dir):
            messagebox.showerror("错误", f"源目录 '{root_dir}' 不存在或不是一个目录。")
            return
        if not output_file:
            messagebox.showerror("错误", "请输入输出文件名。")
            return
        
        # Check if output file directory exists, if not, offer to create
        output_dir = os.path.dirname(output_file)
        if output_dir and not os.path.exists(output_dir):
            if messagebox.askyesno("创建目录?", f"输出目录 '{output_dir}' 不存在。是否创建它?"):
                try:
                    os.makedirs(output_dir, exist_ok=True)
                except OSError as e:
                    messagebox.showerror("错误", f"无法创建目录 '{output_dir}': {e}")
                    return
            else:
                return # User chose not to create

        self.log_text.delete(1.0, tk.END) # Clear previous logs
        self.log_message("开始收集过程...")
        self.start_button.config(state=tk.DISABLED)
        self.browse_dir_button.config(state=tk.DISABLED)
        self.browse_output_button.config(state=tk.DISABLED)
        self.progress_bar['value'] = 0

        # Run the collection in a separate thread to keep GUI responsive
        thread = threading.Thread(
            target=self._run_collection_task,
            args=(root_dir, output_file)
        )
        thread.daemon = True # Allow main program to exit even if thread is running
        thread.start()

    def _run_collection_task(self, root_dir, output_file):
        try:
            collect_cs_files_content_gui(root_dir, output_file, self.log_message, self.update_progress)
        except Exception as e:
            self.log_message(f"发生未预料的错误: {e}")
            self.update_progress(-1) # Indicate error
        finally:
            # Ensure GUI elements are re-enabled from the main thread
            if self.master.winfo_exists():
                self.master.after(0, self._enable_controls)

    def _enable_controls(self):
        if self.master.winfo_exists():
            self.start_button.config(state=tk.NORMAL)
            self.browse_dir_button.config(state=tk.NORMAL)
            self.browse_output_button.config(state=tk.NORMAL)
            if self.progress_bar['value'] == 100:
                 messagebox.showinfo("完成", "文件收集完成！")
            elif self.progress_bar['value'] == -1 or self.progress_bar['value'] == 0 and "错误" in self.log_text.get(1.0, tk.END):
                 messagebox.showerror("错误", "文件收集中发生错误，请查看日志。")


def main():
    root = tk.Tk()
    app = CsCollectorApp(root)
    root.mainloop()

if __name__ == "__main__":
    main()