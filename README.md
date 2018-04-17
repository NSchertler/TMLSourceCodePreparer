TML Source Code Preparer
==

[![Build status](https://ci.appveyor.com/api/projects/status/c3kga7i87nv5qios?svg=true)](https://ci.appveyor.com/project/NSchertler/tmlsourcecodepreparer)

Preparing source code for student tasks can be tedious because the source code has to be maintained in many different versions (student version, solution, potentially for multiple interdepending tasks).
The TML source code preparer allows you to maintain all possible versions within a single valid and functional source code file.
The idea is to augment the source code with XML-like annotations that let you activate and deactivate parts of the code.
At this point, the source code preparer supports all C-like languages.

The source code preparer is available as a Windows .Net program.
Unix and Mac users may be able to adapt the code to be usable with Mono.
Feedback will be greatly appreciated.

This program is still in beta status.
No guarantees can be made.
All modified files will be backed up before changes are made.

The most recent build can be accessed through [releases](releases).

Example
--

Let us assume the student's task is to implement a C++ function that calculates the factorial and the teacher provides the following code fragment:

	#include <iostream>

	long long int factorial(int n)
	{
		//Task: Implement factorial
		return 0;
	}

	int main()
	{
		std::cout << factorial(10) << std::endl;
		return 0;
	}
	
The corresponding solution could like like this:

	#include <iostream>

	long long int factorial(int n)
	{
		if(n <= 1)
			return 1;
		else
			return n * factorial(n - 1);
	}

	int main()
	{
		std::cout << factorial(10) << std::endl;
		return 0;
	}
	
Instead of maintaining two separate versions, TML allows you to have both parts within a single file:

	#include <iostream>

	//<snippet task="1">
	//<student>
	////Task: Implement factorial
	//return 0;
	//</student>
	//<solution>
	long long int factorial(int n)
	{
		if(n <= 1)
			return 1;
		else
			return n * factorial(n - 1);
	}
	//</solution>
	//</snippet>

	int main()
	{
		std::cout << factorial(10) << std::endl;
		return 0;
	}
The variable part is encapsulated between `<solution></solution>` tags and the student solution is currently commented out.
Hence, the file can still be compiled with a standard C++ compiler and represents the solution version.
TML allows you to quickly switch between versions (by altering the commenting) and to generate clean source files for distribution.

TML
-

In order to use TML, you have to add XML-like tags to the source code.
Every tag begins with the line comment symbol (`//`).
In the following, you find a reference of possible tags.

The only tag that is allowed without any parent tag is the `//<snippet>` tag.
This tag marks a variable portion of the code.
The user can optionally specify the task number that the snippet belongs to.
The task number must be an integer but can be separated into up to three levels, each separated by `.`.
The following examples are valid task numbers:

    3
	6.2
	9.2.5
	
Each `//<snippet>` can have three possible subtags.
All subtags are optional (although it does not make sense to have an empty `//<snippet>`.
All possible subtags represent one version of the code.
The corresponding code text is the only content of these subtags (see example above).
The possible subtags are:

    student
	solution
	specialsolution
The meaning of these subtags will be outlined below.
	
At any point, only one of these subtags must be active.
I.e., the code of all inactive subtags must be commented out with line comments (`//`).
Block comments (`/* ... */`) will not be recognized.
The TML runtime will add and remove line comments when switching between versions.
It is necessary that the runtime can determine if a subtag is active at any time.
A subtag will be considered active if there is at least one uncommented non-empty line.
Therefore, if the only content of one of the versions is a comment, use block comments.
Otherwise, the line comment will be recognized as inactive code and the TML runtime may remove the line comments:

	//WRONG: Do not use line comments for comment-only code
    //<student>
	////Task: Implement factorial
	//</student>
	
	CORRECT: Use block-comments
	//<student>
	///*Task: Implement factorial*/
	//</student>

TML Processing
-

To make use of your TML markup, you can use the utility program of this repository.
This program allows you to do in-place transformations of the code files (e.g. for switching between versions) and to create out-of-place copies with a specific version.
The following section will explain its usage.

When you start the application, the following window will appear:

[![Application Screenshot][1]][1]

### 1. Source Folder

The utility will process all files within a given source folder, including any subdirectories.
Choose this folder in this step (use the `...` button to open a folder dialog).

### 2. File Extensions

Only files with certain extensions will be processed.
List all relevant extensions in this text box (separated by a space).

### 3. Operation

This step lets the user select the operation to perform.

In-place transformations change the source files to a specified version.
The specified subtag (student version or solution) will be made active (i.e. not commented).
All other subtags will be commented out.
Remember that the TML runtime determines if a subtag is active by the presence of line comments (`//`) in front of every non-empty line.
In-place transformations are helpful to quickly test a specific version of the code.

Create operations will create out-of-place versions of the source code in a specified directory (see step 4).
This is helpful for distributing a clean version of the code.

**Use special solution if available**: Check this option if you want to use `//<specialsolution>` subtags if they are available.
If these subtags exist within a snippet, they are used in place of the `//<solution>` subtags.
This is helpful if you want to provide the solution both in source code form and in compiled form but both are slightly different (e.g. shaders are embedded in the compiled file).

**Solutions only up to task**:
If you want to create a partial solution, you can specify a task number, up to which solutions are used.
The TML runtime will use the student version for all snippets beyond this task number.

Consider the following example with code that has snippets for tasks 1.1, 1.2, 2.1, 2.2, 2.3, 3.1.
If you specify *solutions up to task 2*, the generated files will contain solutions for tasks 1.1, 1.2, 2.1, 2.2, and 2.3.
And it will contain the student version for task 3.1.
If you use the *only transformed files in output* option (see step 4), the output directory will contain only files that have snippets for tasks 2.1, 2.2, and 2.3.

### 4. Output Folder

Choose the folder in which to generate the created version.
This does not apply to in-place transformations.
If you check **Only transformed files in output**, the output folder will not contain files that do not have any `//<snippet>` tags.
Furthermore, if you are creating the solution version, it will contain only files that have snippets with the specified task number (including possible subtasks).

### Start Transformation

Click this button to begin processing.

### Backup Folder

Whenever the source folder changes, the files within the folder are backed up to the shown location.
Click the `...` button to open the folder.

  [1]: doc/screenshot.png