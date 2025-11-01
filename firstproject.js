// 1
let name = prompt("Enter your name:");
alert("Hello, " + name + "!");

// 2
let num = parseInt(prompt("Enter a number:"));
let square = num * num;
alert("The square of the number is: " + square);

// 3
let n1 = parseInt(prompt("Enter the first number:"));
let n2 = parseInt(prompt("Enter the second number:"));
let avg = (n1 + n2) / 2;
alert("The average is: " + avg);

// 4
let side = parseInt(prompt("Enter the side length of the square:"));
let area = side * side;
alert("The area of the square is: " + area);

// 5
const KMM = 0.621371;
let km = parseInt(prompt("Enter distance in kilometers:"));
let miles = km * KMM;
alert(km + " km = " + miles + " miles");

// 6
let a = parseFloat(prompt("Enter the first number:"));
let b = parseFloat(prompt("Enter the second number:"));

alert("Sum: " + (a + b));
alert("Difference: " + (a - b));
alert("Product: " + (a * b));
if (b !== 0) {
  alert("Quotient: " + (a / b));
} else {
  alert("Error: Division by zero!");
}

// 7
let A = parseFloat(prompt("Enter a:"));
let B = parseFloat(prompt("Enter b:"));
if (A !== 0) {
  let X = -B / A;
  alert("Solution: x = " + X);
} else {
  alert("No solution (a cannot be 0).");
}

// 8
let hours = parseInt(prompt("Enter current hour (0-23):"));
let minutes = parseInt(prompt("Enter current minutes (0-59):"));
let totalMinutesLeft = (23 - hours) * 60 + (60 - minutes);
let hLeft = Math.floor(totalMinutesLeft / 60);
let mLeft = totalMinutesLeft % 60;
alert("Time left until midnight: " + hLeft + " hours and " + mLeft + " minutes.");

// 9. 
let num3 = parseInt(prompt("Enter a three-digit number:"));
let secD = Math.floor((num3 % 100) / 10);
alert("The second digit is: " + secD);

// 10.
let num5 = parseInt(prompt("Enter a five-digit number:"));
let last = num5 % 10;
let rest = Math.floor(num5 / 10);
let newNumber = last * 10000 + rest;
alert("The new number is: " + newNumber);

// 11.
let sales = parseFloat(prompt("Enter the total sales for the month ($):"));
let salary = 250 + sales * 0.1;
alert("Employee's salary: $" + salary);