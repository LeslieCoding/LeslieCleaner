package main

import (
	"encoding/json"
	"fmt"
	"os"
)

type Task struct {
	ID        int    `json:"id"`
	Title     string `json:"title"`
	Completed bool   `json:"completed"`
}

type TodoList struct {
	Tasks []Task `json:"tasks"`
}

const filename = "todos.json"

func loadTasks() (*TodoList, error) {
	if _, err := os.Stat(filename); os.IsNotExist(err) {
		return &TodoList{Tasks: []Task{}}, nil
	}

	data, err := os.ReadFile(filename)
	if err != nil {
		return nil, err
	}

	var list TodoList
	err = json.Unmarshal(data, &list)
	if err != nil {
		return nil, err
	}

	return &list, nil
}

func saveTasks(list *TodoList) error {
	data, err := json.MarshalIndent(list, "", "  ")
	if err != nil {
		return err
	}

	return os.WriteFile(filename, data, 0644)
}

func main() {
	if len(os.Args) < 2 {
		fmt.Println("Usage: todo <command> [arguments]")
		fmt.Println("Commands:")
		fmt.Println("  add <title>      Add a new task")
		fmt.Println("  list             List all tasks")
		fmt.Println("  done <id>        Mark a task as completed")
		fmt.Println("  delete <id>      Delete a task")
		return
	}

	command := os.Args[1]

	list, err := loadTasks()
	if err != nil {
		fmt.Printf("Error loading tasks: %v\n", err)
		return
	}

	switch command {
	case "add":
		if len(os.Args) < 3 {
			fmt.Println("Error: title is required")
			return
		}
		title := os.Args[2]
		newID := 1
		if len(list.Tasks) > 0 {
			newID = list.Tasks[len(list.Tasks)-1].ID + 1
		}
		list.Tasks = append(list.Tasks, Task{ID: newID, Title: title, Completed: false})
		err = saveTasks(list)
		if err != nil {
			fmt.Printf("Error saving task: %v\n", err)
			return
		}
		fmt.Printf("Added task: %s (ID: %d)\n", title, newID)

	case "list":
		if len(list.Tasks) == 0 {
			fmt.Println("No tasks found.")
			return
		}
		fmt.Println("ID  | Status | Title")
		fmt.Println("--------------------")
		for _, task := range list.Tasks {
			status := "[ ]"
			if task.Completed {
				status = "[x]"
			}
			fmt.Printf("%-3d | %-6s | %s\n", task.ID, status, task.Title)
		}

	case "done":
		if len(os.Args) < 3 {
			fmt.Println("Error: ID is required")
			return
		}
		var id int
		_, err := fmt.Sscanf(os.Args[2], "%d", &id)
		if err != nil {
			fmt.Println("Error: invalid ID")
			return
		}

		found := false
		for i := range list.Tasks {
			if list.Tasks[i].ID == id {
				list.Tasks[i].Completed = true
				found = true
				break
			}
		}

		if !found {
			fmt.Printf("Task with ID %d not found\n", id)
			return
		}

		err = saveTasks(list)
		if err != nil {
			fmt.Printf("Error saving task: %v\n", err)
			return
		}
		fmt.Printf("Task %d marked as done\n", id)

	case "delete":
		if len(os.Args) < 3 {
			fmt.Println("Error: ID is required")
			return
		}
		var id int
		_, err := fmt.Sscanf(os.Args[2], "%d", &id)
		if err != nil {
			fmt.Println("Error: invalid ID")
			return
		}

		found := false
		newTasks := []Task{}
		for _, task := range list.Tasks {
			if task.ID == id {
				found = true
				continue
			}
			newTasks = append(newTasks, task)
		}

		if !found {
			fmt.Printf("Task with ID %d not found\n", id)
			return
		}

		list.Tasks = newTasks
		err = saveTasks(list)
		if err != nil {
			fmt.Printf("Error saving task: %v\n", err)
			return
		}
		fmt.Printf("Task %d deleted\n", id)

	default:
		fmt.Printf("Unknown command: %s\n", command)
	}
}
