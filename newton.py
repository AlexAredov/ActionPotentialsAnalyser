from sympy import *
import numpy as np

def flat(x, y, n):
    dat = []
    for i in range(0,len(x),n):
        dat.append((x[i], y[i]))
    return dat

def inter(points):
    n = len(points)
    #создание списка разделенных разностей
    divided_diff = [[0]*n for i in range(n)]
    #считаем коэффиценты ньютона
    for i in range(n):
        divided_diff[i][0] = points[i][1]
    for j in range(1,n):
        for i in range(n-j):
            divided_diff[i][j] = (divided_diff[i+1][j-1] - divided_diff[i][j-1]) / (points[i+j][0]-points[i][0])
    #формируем многочлен ньютона
    formula = f"{divided_diff[0][0]:.2f}"
    for i in range(1, n):
        formula += f" + {divided_diff[0][i]:.2f}"
        for j in range(i):
            formula += f"(x - {points[j][0]:.2f})"
    #формированеи функции
    def newton(x):
        result = divided_diff[0][0]
        for i in range(1, n):
            term = divided_diff[0][i]
            for j in range(i):
                term *= (x - points[j][0])
            result += term
        return result
    return formula, newton


points = [(1,2), (2,4), (3,8)]
file = open('./17.txt')
x_t = []
y_t = []
for i in file:
    x_temp = float(i.split('\t')[0].replace('\n', '').replace(',', '.'))
    y_temp = float(i.split('\t')[1].replace('\n', '').replace(',', '.'))
    x_t.append(x_temp)
    y_t.append(y_temp)

#writing just the right values
x = []
y = []
for i in range(len(x_t)):
    if (x_t[i] < x_t[y_t.index(max(y_t))]) and (y_t[i] < min(y_t)/2):
        x.append(x_t[i])
        y.append(y_t[i])

#first flatting (the big one)
l = 32
dff = flat(x, y, 32)

#print(dff)

formula, f = inter(dff)
print(formula)